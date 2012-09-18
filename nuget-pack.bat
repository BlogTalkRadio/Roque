rmdir lib /s /q
.nuget\nuget pack Roque.nuspec -BasePath Roque.Service\bin\Release -Verbose -Build -Prop Configuration=Release
.nuget\nuget pack Roque.Worker.nuspec -BasePath Roque.Service\bin\Release -Verbose -Build -Prop Configuration=Release