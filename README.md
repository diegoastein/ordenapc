# OrdenaPC

OrdenaPC es una app para Windows 10 y 11 que ordena archivos sola. Vigila carpetas como Escritorio, Descargas o Documentos y mueve cada archivo que coincide con una regla a su carpeta destino, normalmente una subcarpeta de Google Drive.

- Es un solo `.exe` de unos 70 KB y no hay que instalar nada, porque usa .NET Framework 4.8, que ya viene con Windows 10 y 11. No necesita permisos de administrador.
- Nunca borra nada, solo mueve. Si en el destino ya hay un archivo con el mismo nombre, le agrega la fecha y la hora (`epicrisis_juanperez_2026-09-24_14-32.docx`).
- Si el destino no está disponible, por ejemplo con Google Drive desconectado, el archivo queda donde estaba y se reintenta cada 5 minutos.
- Guarda un log CSV de cada movimiento y permite deshacer un movimiento desde la ventana del log.

## Descarga

Bajá `OrdenaPC.exe` desde la sección **Releases** del repo. Si el navegador bloquea el `.exe`, bajá `OrdenaPC.zip` y descomprimilo.

> Como el `.exe` no está firmado, Windows SmartScreen puede mostrar el aviso "Windows protegió su PC". Para seguir, hacé clic en **Más información** y después en **Ejecutar de todas formas**.

## Cómo funciona

- **Reglas.** Cada regla tiene carpeta origen, extensiones, palabras clave, carpeta destino y un interruptor para activarla. Un archivo coincide cuando su extensión está en la lista y su nombre contiene alguna de las palabras clave, sin distinguir mayúsculas ni acentos. Si coincide con varias reglas, se aplica la primera de la tabla. Los archivos que no coinciden con ninguna no se tocan.
- **Qué archivos mira.** Solo los que están directamente en la carpeta origen, no los de subcarpetas. Ignora temporales (`~$…`, `.tmp`, `.crdownload`), archivos ocultos y accesos directos.
- **Cuándo actúa.** Mueve en tiempo real cuando aparece un archivo. Además hace un barrido de respaldo cada 15, 30, 60 o 120 minutos, configurable.
- **Espera antes de mover.** Un archivo se mueve recién cuando pasó un tiempo sin modificarse (5 minutos por defecto; se puede poner entre "Sin espera" y 60 minutos). Así podés seguir corrigiendo un documento que acabás de guardar.
- **Deshacer.** Desde el ícono de la bandeja, "Deshacer: archivo" devuelve el último archivo movido a su carpeta original. Desde la ventana del log se puede deshacer cualquier movimiento. Un archivo devuelto no se vuelve a mover solo.
- **Primer barrido.** Hasta que revises la simulación de "qué movería", la vigilancia automática no mueve nada. También podés activarla sin simular.
- **Buzones.** Son recuadros de color en el escritorio, con nombre, color, tamaño, posición y carpeta destino configurables. Lo que se arrastra encima va a esa carpeta, sin importar su nombre, y el botón "Abrir carpeta" lleva al destino. Si la carpeta no está disponible, el archivo queda guardado en la PC y se envía solo después. Desde un pendrive, el archivo se copia en lugar de moverse. Para cambiar su lugar o tamaño, usá "Acomodar en el escritorio" en la pestaña Buzones.
- **Limpieza.** Por cada carpeta elegida, los archivos que no coinciden con ninguna regla y llevan más de N días sin cambios van a una carpeta destino, opcionalmente separados por mes. Nunca se borran. Al crear una limpieza, primero se muestra qué movería.
- **PIN de administrador.** Si hay un PIN configurado, se pide para abrir el panel, pausar, deshacer o cerrar la app. Una vez ingresado, vale 5 minutos, y mientras el panel está abierto no vence. Si te lo olvidás, borrá `PinHash` y `PinSalt` de `config.json`.
- **Reapertura automática.** Una tarea programada del usuario, `OrdenaPC-Vigilancia`, abre la app cada 5 minutos si alguien la cerró sin usar "Salir". Si se cerró con "Salir", no se vuelve a abrir hasta que la abras vos o se reinicie la PC.
- **Alerta.** Si una carpeta destino, como Google Drive, lleva sin responder más del tiempo configurado (3 horas por defecto), aparece una ventana grande que indica a quién avisar.
- **Datos.** La configuración y el log están en `%APPDATA%\OrdenaPC\` (`config.json` y `log.csv`). El log se abre en Excel.
- **Instalación.** Al abrir el `.exe` por primera vez, ofrece copiarse a `%LOCALAPPDATA%\OrdenaPC\` y arrancar con Windows, usando la clave `HKCU\...\Run`.

## Prueba manual (checklist)

1. Abrí `OrdenaPC.exe` y aceptá instalarlo. Tiene que aparecer el ícono azul con una "O" junto al reloj.
2. Agregá una regla de prueba: origen **Escritorio**, extensión `.txt`, palabra clave `prueba` y como destino una carpeta de Drive, por ejemplo `G:\Mi unidad\Prueba`.
3. Seleccionala y usá **Probar regla**. Tiene que mostrar la lista sin mover nada.
4. Revisá el primer barrido y activalo.
5. Creá `prueba1.txt` en el Escritorio. Unos segundos después tiene que estar en Drive y aparecer la notificación, si está configurada.
6. Creá otro `prueba1.txt`. Tiene que llegar como `prueba1_AAAA-MM-DD_HH-mm.txt`.
7. Pausá Google Drive, o poné un destino en una unidad que no existe, y creá `prueba2.txt`. El archivo tiene que quedar en el Escritorio y el log tiene que mostrarlo como **Pendiente**. Cuando Drive vuelve, se mueve solo en menos de 5 minutos.
8. En **Ver log**, seleccioná un movimiento y usá **Deshacer seleccionado**. El archivo tiene que volver al Escritorio y quedarse ahí.
9. Reiniciá Windows. OrdenaPC tiene que arrancar solo.

## Desarrollo

- `src/OrdenaPC.Core`: la lógica, con reglas, movimiento seguro, log y barridos. Compila para `net48` y `net8.0`.
- `src/OrdenaPC`: la interfaz WinForms, con bandeja, panel, watcher e instalación. Es `net48` e incluye el código del Core, así que queda un solo `.exe`.
- `tests/OrdenaPC.Tests`: los tests con xUnit, que corren en `net8.0` y `net48`.

GitHub Actions compila en `windows-latest` con cada push. Para publicar una versión, creá un tag `vX.Y.Z` y subilo.
