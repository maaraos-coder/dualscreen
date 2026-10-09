# DualScreen Portable — prototipo v2

Aplicación WinForms (.NET 8) para Windows 11 x64. **No requiere PowerShell ni derechos de administrador por diseño**. Compilación self-contained con GitHub Actions.

## Qué hace esta versión
- Divide el área útil del monitor principal en zonas 50/50.
- Ubica/limita ventanas estándar en su zona mediante API Win32 (revisión ~0,9 s).
- Presenta dos **barras personalizadas**, sobre la barra original de Windows, con botones para activar ventanas, pausar y salir.
- Para restaurar el manejo normal de las ventanas, pulse **Pausar** (no restaura automáticamente las posiciones anteriores).

## Limitaciones importantes
- **NO crea dos pantallas virtuales reales ni dos barras nativas de Windows.** Windows mantiene su barra real a lo ancho.
- Algunas ventanas especiales, elevadas, minimizadas, juegos, ventanas modales y programas a pantalla completa no se administran.
- Las ventanas maximizadas se restauran y ajustan: no se modifica la semántica de maximización interna de Windows.
- Si se cierra la aplicación, las ventanas quedan donde estaban; se pueden maximizar manualmente como siempre.
- Puede requerir autorización TI para ejecutar el .exe aunque no requiera instalación.
- Prototipo para pruebas, **no validado en una máquina Windows real**.

## Generar archivo portable usando GitHub
1. Cree un repositorio nuevo, por ejemplo `DualScreenPortable`, en GitHub.
2. Suba a la raíz del repositorio **todos los archivos y carpetas de este ZIP descomprimidos**, incluido `.github/workflows/build.yml` (la carpeta `.github` debe conservarse).
3. Abra **Actions → Compilar DualScreen para Windows x64 → Run workflow** (también se lanza al subir a `main`).
4. Cuando finalice, abra el run y descargue el artefacto `DualScreen-Windows-x64`.
5. Descomprima el ZIP del artefacto y después el archivo `DualScreen-Windows-x64.zip` que contiene; ejecute `DualScreen.exe`.

No ejecute comandos para cambiar ExecutionPolicy ni otras directivas de seguridad. Si Windows bloquea el .exe, consulte con TI.
