# example-plugin
An example plugin for ETS2LA C#. You can use this as a starting point for you own.

**Note**: This project provides built in tasks for `VSCode`. You can take a look at `.vscode/tasks.json` to see how they work, and modify them to work with your own IDE.

# Usage
- Clone/Fork the repository, this will also clone ETS2LA as a submodule to `./ETS2LA`. Make sure your IDE is configured to also clone the submodules (or git with `--recurse-submodules`). You can do this manually by running `git submodule update --init --recursive` after cloning, or by checking the "Initialize submodules" option when cloning in GitHub Desktop.
- Open the solution in your IDE and run the project. This will boot up ETS2LA with your plugin loaded.
- Your plugin files will be added to `ProjectName/bin/Debug/net.10.0`, copying the .DLLs from here to `./Plugins` in a live ETS2LA instance will also "install" your plugin.
- We've also provided `ETS2LA.bat/sh` which will build ETS2LA, then all your plugins, and then run ETS2LA with the plugins copied to the right place. You can modify this script to your needs (for example copy any other libraries that your plugin needs to the output folder as well).

**Note**: ETS2LA C# does not yet provide a plugin repository, you will need to install plugins via copying the .DLLs to /Plugins.

**Note**: If you want to reload your plugins *without* restating ETS2LA, **first unload them in the plugin manager**, and then run `BuildYourPlugins.bat/sh` to build and copy the new .DLLs to the plugins folder, then reload them in the plugin manager.

**Note**: Linux users, remember to allow execution of the `.sh` files with `chmod +x filename.sh` or via your file manager!

# Example Workflow
1. Run the task `Run ETS2LA` to start ETS2LA with your plugin installed.
2. Do any changes to your plugins' code.
3. Unload all plugins in the plugin manager in ETS2LA.
4. Run the task `Build your projects, update .DLLs in ETS2LA` to build your plugins and copy their .DLLs to ETS2LA's plugins folder.
5. Reload your plugins in the plugin manager in ETS2LA.

# Adding more plugins
You can add more plugins to your repository by cloning the `ExamplePlugin` project and renaming it. Each project can also contain multiple plugins, so you are not limited to one per project namespace. 

# API
For further information please see the `rewrite` branch documentation on the [ETS2LA Repo](https://github.com/ETS2LA/Euro-Truck-Simulator-2-Lane-Assist/tree/rewrite). The API is still in development and subject to change, but the documentation should be up to date with the latest changes.

We're also working on https://docs.ets2la.com which will have more detailed API documentation and guides.
