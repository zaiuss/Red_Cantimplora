# Android — App Cantimplora

Aplicación Android (`.NET MAUI`, solo `net9.0-android`) que actúa como
terminal Bluetooth SPP para hablar con un nodo ESP32 de la red Cantimplora.
Lista los dispositivos emparejados, se conecta al que elijas, y muestra
un chat con los mensajes LoRa entrantes y salientes.

## Tecnologias utilizadas

- .NET 9, `net9.0-android`, MAUI 9.
- `CommunityToolkit.Mvvm` 8.4.0 (MVVM, `[ObservableProperty]`,
  `[RelayCommand]`).
- `sqlite-net-pcl` 1.9.172 (persistencia).
- `Microsoft.Extensions.Logging.Debug` 10.0.0.
- XAML source generation (`MauiXamlInflator=SourceGen`).
- Solo Android. Sin `#if ANDROID` dentro de `Services/`.

## Arquitectura

La app sigue el patrón MVVM con inyección de dependencias. La capa de
UI (páginas XAML) no accede directamente a Bluetooth, a la base de
datos ni a las APIs de Android: habla siempre con el ViewModel, que a
su vez delega en servicios detrás de interfaces. El ViewModel es un
singleton que orquesta el ciclo de vida de la conexión y la lógica
del chat, y los detalles de plataforma (toasts, navegación, permisos)
viven en servicios independientes que se inyectan.

El código incluye comentarios generados por una IA en la mayoría de los
métodos. Tratarlos como notas orientativas, no como spec.

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

## Instalar y compilar

Si solo quieres probar la app en tu móvil, ve a la pestaña
[Releases](https://github.com/Zaiuss/red_cantimplora/releases) del repo
y descarga `com.ojetedevs.cantimplora_release_1.2.apk`. No necesitas
Visual Studio ni el .NET SDK instalados.

Requisitos en el móvil: hay que permitir orígenes desconocidos en
Ajustes -> Seguridad para instalar APKs de fuera de Play Store. Si
ya hay una versión anterior con firma distinta, desinstalar antes:
Android no deja actualizar entre firmantes distintos.

Si prefieres compilar tú mismo, necesitas Visual Studio con el
workload de .NET MAUI 9 instalado:

Debug (sin firmar, para desarrollo):

```bash
dotnet build Cantimplora.csproj -f net9.0-android
```

Release firmada, lista para distribuir:

```bash
dotnet publish Cantimplora.csproj -f net9.0-android -c Release
```

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
