# Giraffe File Upload

Start the server like this:

```bash
dotnet tool restore
dotnet paket restore
dotnet run
```

Send data like this:

```bash
curl -v -X POST --data-binary @big-file.csv localhost:8080
```

Or form encoded:

```bash
curl -v -X POST -F records=@big-file.csv localhost:8080/form
```

You can generate test data with:

```bash
dotnet fsi ./GenerateCsv.fsx
```
