# WebGPU-Wrappers-for-Silk.Net
Experimental "safe" wrappers around the unsafe Silk.NET bindings

## Building
- Create your own Silk.NET nuget packages (the ones hosted on nuget haven't been updated as of the time I'm writing this)
  - follow the [build instrunctions on the Silk.NET repo](https://github.com/dotnet/Silk.NET/tree/main/#building-from-source)
  - run the command for packing nuget packages e.g. `nuke pack`
  - packages will be outputed to `<Silk.Net-Folder>/build/output_packages`
 
- Prepare and Build this project
  - clone this repository
  - run `dotnet restore --packages <Silk.Net-Folder>/build/output_packages`
  - run `dotnet build`
