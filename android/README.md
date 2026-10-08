# Android — App Cantimplora

Aplicación Android (`.NET MAUI`, solo `net9.0-android`) que actúa como
terminal Bluetooth SPP para hablar con un nodo ESP32 de la red Cantimplora.
Lista los dispositivos emparejados, se conecta al que elijas, y muestra
un chat con los mensajes LoRa entrantes y salientes.

## Qué hace

- Lista los dispositivos Bluetooth emparejados del sistema.
- Conecta al ESP32 seleccionado por MAC usando el perfil SPP
  (`00001101-0000-1000-8000-00805F9B34FB`).
- Muestra un chat con los mensajes LoRa (`RX src=…`) entrantes y permite
  enviar mensajes propios (`TX`).
- Cabecera con menú `⋮`, etiqueta de estado + MAC + `NODO_<id>`, botón
  `⟳` (refresh) cuando no hay conexión, y switch on/off.
- Página de configuración del ESP32 (`/tx`, `/gap`) y filtro de debug
  para mensajes de sistema y comandos.
- Persistencia local en SQLite: historial de chat por MAC y ajustes
  (`TxRepeat`, `GapMs`).
- Overlay de "Buscando dispositivos..." mientras se ejecuta el refresh y
  "Desconectando..." mientras se desconecta.
- Store-and-forward implícito: si el nodo tenía mensajes encolados al
  desconectarse, los recibes al conectar (gestionado por el firmware).

## Stack

- .NET 9, `net9.0-android`, MAUI 9.
- `CommunityToolkit.Mvvm` 8.4.0 (MVVM, `[ObservableProperty]`,
  `[RelayCommand]`).
- `sqlite-net-pcl` 1.9.172 (persistencia).
- `Microsoft.Extensions.Logging.Debug` 10.0.0.
- XAML source generation (`MauiXamlInflator=SourceGen`).
- Solo Android. Sin `#if ANDROID` dentro de `Services/`.

## Arquitectura

MVVM con inyección de dependencias. El flujo va de la capa de UI a la de
datos, con un ViewModel singleton que orquesta las piezas.

```
View (XAML + code-behind)
  └── Pages/Esp32Page        UI principal: header + chat + entry
  └── Pages/Esp32ConfigPage  config de /tx, /gap, debug

ViewModel
  └── ViewModels/Esp32ViewModel   singleton, implementa los coordinadores
  └── ViewModels/ChatMessage      entidad in-memory para CollectionView

Services (todo detrás de interfaces, registrado en MauiProgram.cs)
  ├── IBluetoothService / BluetoothService     SPP Android
  ├── IMessageStore / MessageStore (SQLite)    tablas Messages y Settings
  ├── SettingsService                          TxRepeat / GapMs
  ├── Esp32LineParser / Esp32StatParser        parsers puros
  ├── ChatFilter / ChatBuffer                  regla de visibilidad + buffer
  ├── IChatCoordinator / IEsp32ConfigCoordinator
  ├── INavigationService / IPermissionService / IToastService

Models
  ├── BluetoothDevice       emparejado (Name, MacAddress)
  ├── StoredChatMessage     fila de Messages
  └── StoredSetting         fila de Settings (key-value)
```

Patrones clave:
- Coordinadores por página. `Esp32Page` consume `IChatCoordinator`,
  `Esp32ConfigPage` consume `IEsp32ConfigCoordinator`. El VM implementa
  ambas y expone los commands como propiedades explícitas para evitar
  mismatch de tipos entre `RelayCommand` y `ICommand`.
- Filtros como funciones puras. `ChatFilter.ShouldBeVisible(kind, isCommand)`
  decide qué se ve. `ChatBuffer` encapsula la doble lista
  (`_allMessages` completa, `Messages` filtrada).
- Servicios de plataforma inyectados. Ni el VM ni las páginas usan
  `Shell.Current`, `Android.Widget.Toast` ni `BluetoothPermissionHelper`
  directamente.

## Estructura

```
android/
├── Cantimplora.csproj
├── Cantimplora.slnx
├── App.xaml(.cs)              # App MAUI
├── AppShell.xaml(.cs)         # Shell con Esp32Page y Esp32ConfigPage
├── MauiProgram.cs             # bootstrap + DI
├── cantimplora-release.keystore
├── Pages/
│   ├── Esp32Page.xaml(.cs)    # UI principal
│   └── Esp32ConfigPage.xaml(.cs)
├── Views/                     # componentes reutilizables
│   ├── StatusOverlay, BtDeviceItemView, ChatBubbleView,
│   ├── ActiveLinkView, InactiveLinkView,
│   └── Esp32DeviceListView, Esp32ComposerView
├── ViewModels/
│   ├── Esp32ViewModel.cs
│   └── ChatMessage.cs
├── Services/                  # ver Arquitectura
├── Models/
│   ├── BluetoothDevice.cs
│   ├── StoredChatMessage.cs
│   └── StoredSetting.cs
├── Platforms/Android/         # MainActivity, AndroidManifest, BluetoothPermissionHelper
├── Resources/                 # iconos, splash, fuentes, imágenes, estilos
└── Properties/launchSettings.json
```

## Persistencia

Base de datos SQLite en `FileSystem.AppDataDirectory/cantimplora.db3`.
Dos tablas:

- **Messages**: `Id` (PK auto), `Mac` (indexed), `Direction`, `Source`,
  `Text`, `Timestamp`. Se cargan los últimos 500 al conectar, ordenados
  por `Id` ASC.
- **Settings**: `Key` (PK), `Value`. Aquí viven `TxRepeat` (repeticiones
  por mensaje) y `GapMs` (ms entre repeticiones), con defaults 4 y 1000.

`ClearAllAsync` borra todas las `Messages` pero **no** borra `Settings`.

## Identidad del nodo LoRa

Al conectar, después del handshake SPP, la app manda `/stat` y parsea la
respuesta. De ahí saca:

- `id` (hex de 2 bytes, `0x01..0xFE`).
- `tx` (repeticiones configuradas en el nodo).
- `gap` (ms entre repeticiones).

Los tres se persisten en SQLite. El `id` se muestra en la cabecera del
chat como `NODO_XX` y se usa como `Source` en los mensajes TX propios
(queda `NODO_XX (Tx)`). Si por la razón que sea el id no se rellena
(timeout, error), el header muestra `NODO_ (Tx)` con el id vacío.

Timeout de la consulta de `/stat`: 5 segundos.

## Permisos

Declarado en `Platforms/Android/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
```

En runtime (Android 12+) la app pide `BLUETOOTH_CONNECT` antes de listar
dispositivos. No se pide `ACCESS_FINE_LOCATION` ni `BLUETOOTH_SCAN` porque
la app solo lista emparejados y se conecta a uno, no escanea nuevos.

`MainActivity` lleva `WindowSoftInputMode = SoftInput.AdjustResize` para
que el teclado no tape el contenido del chat.

## Configuración del `.csproj`

- `ApplicationId`: `com.ojetedevs.cantimplora`.
- `ApplicationTitle`: `Cantimplora`.
- `ApplicationDisplayVersion`: `1.2`. `ApplicationVersion`: `2`.
- `SupportedOSPlatformVersion`: 21.0.
- Keystore release: `cantimplora-release.keystore` en la raíz, alias
  `cantimplora`, contraseña `Cantimplora2024` (key y store, 10.000 días
  de validez). Las contraseñas están en el `.csproj` en texto plano.

## Releases

Si solo quieres probar la app en tu móvil, ve a la pestaña
[Releases](https://github.com/Zaiuss/red_cantimplora/releases) del repo
y descarga `com.ojetedevs.cantimplora_release_1.2.apk`. No necesitas
Visual Studio ni el .NET SDK instalados.

Pasos en el móvil:

1. Activa "Orígenes desconocidos" en Ajustes -> Seguridad.
2. Abre el `.apk` desde el explorador de archivos y acepta la instalación.
3. Si ya tienes una versión anterior con firma distinta, desinstálala
   primero (Android no permite actualizar entre firmantes distintos).

Si quieres tocar el código o compilar tú mismo, sigue en
[Cómo compilar](#cómo-compilar) más abajo.

## Cómo compilar

### Debug

```bash
dotnet build Cantimplora.csproj -f net9.0-android
```

APK en `bin/Debug/net9.0-android/`.

### Release firmada

```bash
dotnet publish Cantimplora.csproj -f net9.0-android -c Release
```

APK firmada en `bin/Release/net9.0-android/com.ojetedevs.cantimplora-Signed.apk`
y en `bin/Release/net9.0-android/publish/`.

## Cómo instalar

1. Copia la APK al móvil.
2. En el móvil: Configuración -> Seguridad -> activa "Orígenes
   desconocidos" (o "Instalar apps de orígenes desconocidos").
3. Abre el `.apk` desde el explorador de archivos.
4. Acepta la instalación.

Si ya hay una versión anterior con firma distinta, desinstálala primero
(Android no permite actualizar entre firmantes distintos).

## Protocolo con el firmware

La app habla con el nodo por Bluetooth SPP usando líneas UTF-8 terminadas
en `\n`. Lo que sale y entra:

- **Hacia el nodo**: texto plano o comando `/xxx`. Texto > 198 bytes
  devuelve `ERR linea demasiado larga`. Líneas vacías se ignoran.
- **Desde el nodo**:
  - `RX src=XX msgId=N seq=M len=L payload="…"` — mensaje DATA aceptado
    de otro nodo.
  - `OK id=XX` — respuesta a `/id`.
  - `OK id=XX seq=N mesh=ON buf=N/32 bt=ON tx=A/Bms` — respuesta a
    `/stat`. La app parsea `id`, `tx` y `gap` de aquí.
  - `ERR …` — error.

## Filtro de debug

Configurable en `Esp32ConfigPage` con el checkbox "Debug en chat":

- **Activado**: se ven RX, TX, TX-comandos (los `/xxx`), OK y ERR.
- **Desactivado**: solo se ven RX y TX normales. Mandar un comando `/`
  muestra un toast nativo "cmd solo en debug mode" y el comando no se
  envía (ni se guarda en SQLite).

## Colores del chat

Centralizados en `Resources/Styles/Colors.xaml`:

- RX: azul medio `#BBDEFB`.
- TX: verde claro `#C8E6C9`.
- OK: amarillo claro `#FFF9C4`.
- ERR: naranja claro `#FFCCBC`.

El color de marca de la app es `#F26B3A` (icono y splash), con fondo
trigo `#F5DEB3` en el icono y morado en el splash.

## Pendientes / ideas

- **Credenciales del keystore**: están en el `.csproj` en plano. Mover a
  variables de entorno o `keytool` directo.
- **Tests**: no hay. Candidatos claros: `Esp32LineParser.ParseLine`,
  `Esp32StatParser.TryParseStatLine`, `ChatFilter.ShouldBeVisible`,
  buffer y `RebuildMessages`.
- **ClearAll no borra Settings**: `MessageStore.ClearAllAsync` solo borra
  `Messages`. Si se quiere un reset completo, también `Settings`.
- **Versión dinámica**: `ApplicationDisplayVersion` y `ApplicationVersion`
  hardcoded a `1.2` y `2`. Automatizar para builds incrementales.
- **Migración de BD**: si cambia el esquema de `Messages` o `Settings`,
  hay que añadir migración. `sqlite-net-pcl` lo soporta.
- **iOS / Windows**: el proyecto está preparado solo para Android. Para
  añadir otros targets, reintroducir `#if ANDROID` o `partial class`.
- **Limpiar el repo de sobrantes**: `MainPage.xaml/.cs`, `ConfigPage`,
  `MessagesPage` son placeholders del template MAUI sin uso.
- **Filtros adicionales**: por nodo, por rango de tiempo, búsqueda de
  texto.
- **Exportar logs a fichero**: chat a `.txt` o `.csv` para debug.
- **Vista de "logs del sistema"**: una página aparte con los `OK`/`ERR`
  filtrados, accesible aunque debug esté desactivado.
- **Timeout de `/stat` configurable**: hardcoded a 5s en el VM.

## Changelog

### v1.2 (2026)
- APK release firmada generada, lista para instalar.
- Refactor modular: 7 vistas reutilizables extraídas a `Views/`
  (`StatusOverlay`, `BtDeviceItemView`, `ChatBubbleView`,
  `ActiveLinkView`, `InactiveLinkView`, `Esp32DeviceListView`,
  `Esp32ComposerView`).
- Refactor estructural en 5 fases (parsers puros, `ChatFilter` +
  `ChatBuffer`, `SettingsService`, coordinadores por página, servicios
  de plataforma inyectados). El VM pasa de ~520 a ~395 líneas.
- Personalización visual: splash con logo multicolor de 3 cantimploras,
  icono del launcher con cantimplora verde sobre fondo trigo.
- Colores del chat centralizados en `Resources/Styles/Colors.xaml`.
- Cabecera actualizada: glifo `⟳` reemplaza al `+`, switch solo visible
  con conexión, label central con "Conectado a X" + MAC + `NODO_<id>`.
- Overlay "Buscando dispositivos..." con `StatusOverlay` y flag
  `IsRefreshing` en el VM.
- Scroll automático al fondo del chat al cambiar el tamaño del
  `CollectionView` (teclado).
- `WindowSoftInputMode = SoftInput.AdjustResize` en `MainActivity`.
- Bloqueo de comandos en modo no-debug: no se mandan, no se guardan,
  solo toast. `Direction` corregido de `"SYS"` a `"OK"`/`"ERR"` (antes
  rompía el filtrado del histórico).
- `Entry` de `/tx` y `/gap` con `Unfocused` en vez de `Completed` o
  botón "Aplicar".
- `dotnet publish -c Release` con keystore propio.

### v1.0 (anterior)
- Conexión Bluetooth SPP (`00001101-…`).
- Chat con filtro de visibilidad y parser de líneas del ESP32.
- Identidad del nodo LoRa consultada con `/stat` al conectar.
- Persistencia en SQLite con tablas `Messages` (chat por MAC) y
  `Settings` (key-value).
- Header con botón `⋮`, label de estado, MAC, `NODO_<id>` y switch.
- Toast nativo de Android que avisa al mandar un comando sin debug
  activo.
- Diálogo de confirmación para "Clear DB" desde el menú `⋮`.
