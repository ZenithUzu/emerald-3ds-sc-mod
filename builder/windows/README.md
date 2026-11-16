# Azahar Builder for Windows

1. Generate and extract your game with the [original Emerald 3DS builder](https://emerald-3ds.com/).
2. Drop the extracted folder onto **Game folder or file**, or choose
   `Emerald3DS.3dsx` using **Browse**.
3. Set the output file under **Save CXI as** and click **Build CXI**.

This release imports official v0.5.0 Spanish data. The folder must contain
both the original `Emerald3DS.3dsx` and `emerald3ds.pak`, either directly
or under `3ds/emerald3ds`. The Windows app runs without Python.

To install the mod for an existing CXI from this fork, select that CXI
and choose **Set up mod**. The app can export a `load` folder next to the
CXI and install the mod into the selected Azahar user directory.

## Build the installer from source

Prepare a compiled port with embedded data and the matching official pack.
Generate the import profile, then compile the Windows Forms executable
with .NET Framework 4.8 and [makerom](https://github.com/3DSGuy/Project_CTR/releases/tag/makerom-v0.19.0).

```bat
python builder/windows/make-import-profile.py build/upstream/3ds_port C:\path\to\emerald3ds.pak official-es.zip
builder\windows\build.cmd C:\path\to\makerom.exe official-es.zip
```

The executable includes [makerom's MIT license](makerom-LICENSE.txt).
