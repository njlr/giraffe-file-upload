#r "nuget: Bogus"

open System.IO
open Bogus

let targetFile =
  match fsi.CommandLineArgs with
  | [| _; targetFile |] -> targetFile
  | _ -> failwithf "Usage: <target-file>"

let nRows = 100_000_000

async {
  let file = File.Create targetFile

  use writer = new StreamWriter (file)

  let f = Faker ()

  for i in 0..nRows do
    let firstName = f.Name.FirstName ()
    let lastName = f.Name.LastName ()
    let phone = f.Person.Phone
    let email = f.Internet.Email ()
    let password = f.Internet.Password ()
    let ip = f.Internet.IpEndPoint ()

    let cells =
      [
        string i
        firstName
        lastName
        phone
        email
        password
        string ip
      ]

    let line =
      cells
      |> List.map (fun x -> "\"" + x + "\"")
      |> String.concat ", "

    do!
      writer.WriteLineAsync line
      |> Async.AwaitTask

  do!
    writer.FlushAsync ()
    |> Async.AwaitTask

  printfn "Done. "
}
|> Async.RunSynchronously
