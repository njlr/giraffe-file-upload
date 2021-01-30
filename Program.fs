module GiraffeFileUpload.Program

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Text
open System.Threading
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.WebUtilities
open Giraffe
open FSharp.Control.Tasks

[<Literal>]
let TerabyteInBytes = 1000000000000L

let private countLinesAndPrint (stream : Stream) =
  task {
    let encoding = Encoding.ASCII

    use reader = new StreamReader (stream, encoding)

    let mutable keepGoing = true
    let mutable linesRead = 0

    while keepGoing do
      let! maybeLine = reader.ReadLineAsync ()
      let maybeLine = Option.ofObj maybeLine

      match maybeLine with
      | Some line ->
        printfn "%s" line
        linesRead <- linesRead + 1
      | None ->
        keepGoing <- false

    return linesRead
  }




// This section demonstrates how the entire request body can be treated as a stream

let streamBodyHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      let! lines = countLinesAndPrint ctx.Request.Body

      let response =
        {|
          linesRead = lines
          message = sprintf "Received %i line(s) of data. Thanks! " lines
        |}

      return! json response next ctx
    }















// This section demonstrates how we can process that stream as sections for multi-part forms

let private getBoundary (contentType : string) =
  let boundary =
    contentType.Split(' ')
    |> Array.filter (fun entry -> entry.StartsWith("boundary=", StringComparison.InvariantCulture))
    |> Array.head
    |> (fun elem -> elem.Substring("boundary=".Length))

  // Remove quotes
  if boundary.Length >= 2 then
    let firstChar = boundary.Chars 0
    let lastChar = boundary.Chars (boundary.Length - 1)

    if firstChar = '"' &&  lastChar = '"' then
      boundary.Substring(1, boundary.Length - 2)
    else
      boundary
  else
    boundary




let private tryGetName (contentDisposition : string) =
  contentDisposition.Split(';')
  |> Array.filter (fun part -> part.Contains("name"))
  |> Array.tryHead
  |> Option.map (fun part -> part.Split('='))
  |> Option.map Array.rev
  |> Option.bind Array.tryHead
  |> Option.map (fun name -> name.Trim('"'))




let formHandler =
  fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
      if ctx.Request.HasFormContentType
      then
        let boundary = getBoundary ctx.Request.ContentType

        printfn "boundary: %s" boundary

        let mutable linesRead = 0

        let reader = MultipartReader (boundary, ctx.Request.Body)

        let! nextSection = reader.ReadNextSectionAsync ()
        let mutable section = nextSection

        while isNotNull section do
          printfn "Headers: %A" (Seq.toList section.Headers)

          match tryGetName section.ContentDisposition with
          | Some "records" ->

            let! lines = countLinesAndPrint section.Body
            linesRead <- lines

          | _ -> ()

          let! nextSection = reader.ReadNextSectionAsync ()
          section <- nextSection

        printfn "No more sections. "

        let response =
          {|
            linesRead = linesRead
            message = sprintf "Received %i line(s) of data. Thanks! " linesRead
          |}

        return! json response next ctx
      else
        return! RequestErrors.BAD_REQUEST "Not a form request" next ctx
    }






// Here we configure and launch the app

let webApp =
  POST >=> choose [
    route "/" >=> streamBodyHandler
    route "/form" >=> formHandler
  ]

let errorHandler (ex : Exception) (logger : ILogger) =
  logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
  clearResponse >=> setStatusCode 500 >=> text ex.Message

let configureApp (app : IApplicationBuilder) =
  app
    .UseGiraffeErrorHandler(errorHandler)
    .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
  services.AddGiraffe() |> ignore

[<EntryPoint>]
let main _ =
  printfn "PID: %i" (Process.GetCurrentProcess().Id)

  WebHost.CreateDefaultBuilder()
    .UseKestrel(fun opts ->
      opts.Limits.MaxRequestBodySize <- TerabyteInBytes
      opts.Listen(IPAddress.Loopback, 8080))
    .Configure(configureApp)
    .ConfigureServices(configureServices)
    .Build()
    .Run()

  0
