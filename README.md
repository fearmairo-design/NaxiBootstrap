# Naxi Bootstrap v4

Redesigned from the supplied reference: compact navigation, small window controls, one active page host, pill buttons, and a clean dark UI.

Run:
```powershell
dotnet restore
dotnet run
```

Publish:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
