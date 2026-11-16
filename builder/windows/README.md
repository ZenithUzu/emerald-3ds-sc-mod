# Generador para Azahar en Windows

Abre `Emerald3DS-Azahar.exe`, selecciona la carpeta compilada
`build/upstream/3ds_port`, elige la salida y pulsa **Generar CXI**.
El programa genera un Title ID automáticamente. Una clave fija conserva el
ID entre compilaciones de la misma variante.

Puedes exportar el mod en la carpeta `load` o instalarlo directamente en
Azahar o Azahar+. Usa la [compilación desde fuente](../../README.md#building-from-source)
para preparar el juego del idioma elegido antes de empaquetarlo.

El programa está escrito en C# y usa Windows Forms. Para compilarlo en
Windows x64 con .NET Framework 4.8, descomprime makerom para Windows y ejecuta:

```bat
builder\windows\build.cmd C:\ruta\makerom.exe
```

El ejecutable incluye [makerom de Project CTR](https://github.com/3DSGuy/Project_CTR/releases/tag/makerom-v0.19.0)
y su [licencia MIT](makerom-LICENSE.txt).
