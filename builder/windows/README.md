# Azahar Builder for Windows

Open `Emerald3DS-Azahar.exe` and choose an input:

- **Build a CXI.** Select the compiled `emerald3ds.elf`, choose the output
  file name and location, then click **Build CXI**.
- **Set up the mod for an existing game.** Select the CXI built by this fork,
  choose one or both mod destinations, then click **Set up mod**.

The app shows the generated Title ID and full output paths. The export
writes `load/mods/<Title ID>/romfs` next to the CXI. To install on this PC,
choose the Azahar user directory that contains `config`.

Use the [source build instructions](../../README.md#building-from-source)
to prepare the ELF for your chosen game language.

The app uses C# and Windows Forms. To rebuild it on Windows x64 with
.NET Framework 4.8, extract makerom for Windows and run:

```bat
builder\windows\build.cmd C:\path\to\makerom.exe
```

The executable includes [makerom from Project CTR](https://github.com/3DSGuy/Project_CTR/releases/tag/makerom-v0.19.0)
and its [MIT license](makerom-LICENSE.txt).
