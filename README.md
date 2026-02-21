# example-plugin
An example plugin for ETS2LA C#. You can use this as a starting point for you own.

# Usage
- Clone/Fork the repository, this will also clone ETS2LA as a submodule to `./ETS2LA`.
- Open the solution in your IDE and run the project. This will boot up ETS2LA with your plugin loaded.
- Your plugin files will be added to `ProjectName/bin/Debug/net.10.0` (or Release), copying the .DLLs from here to `./Plugins` in a live ETS2LA instance will "install" your plugin.
- We've also provided `ETS2LA.bat` which will build ETS2LA, then all your plugins, and then run ETS2LA with the plugins copied to the right place. You can modify this script to your needs (for example copy any other libraries that your plugin needs to the output folder as well).
- This repository also provides a build task for `VSCode` which will automatically run `ETS2LA.bat`.

**Note**: ETS2LA C# does not yet provide a plugin repository, you will need to install plugins via copying the .DLLs to /Plugins.

# Adding more plugins
You can add more plugins to your repository by cloning the `ExamplePlugin` project and renaming it. Each project can also contain multiple plugins, so you are not limited to one per project namespace. 

# API
For further information please see the `rewrite` branch documentation on the [ETS2LA Repo](https://github.com/ETS2LA/Euro-Truck-Simulator-2-Lane-Assist/tree/rewrite). The API is still in development and subject to change, but the documentation should be up to date with the latest changes.