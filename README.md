# AutoResolution

Aplicación de bandeja para Windows que cambia la resolución del monitor según el estado de alimentación.

## Uso

1. Ejecuta la aplicación. Quedará en el área de notificación; Windows puede colocarla dentro de **Iconos ocultos**.
2. Abre la ventana con doble clic en el icono.
3. Selecciona una resolución para **Con cargador conectado** y otra para **Con batería**.
4. Pulsa **Guardar** y usa **● Encendido** para activar o desactivar los cambios automáticos.
5. En **Con cargador conectado**, configura la resolución y el slider de brillo.
6. En **Con batería**, configura la resolución, el brillo, **Activar modo de ahorro de batería** y **Activar modo oscuro con batería**.
7. Usa los botones **EN/ES** y **Oscuro/Claro** para cambiar el idioma y el tema de la interfaz.
8. El ahorro de batería se activa al desconectar la corriente y se desactiva al conectar el cargador cuando está habilitado.
9. La sección **Tiempo de uso** muestra cuánto tiempo acumulado estuvo el equipo conectado a la corriente y cuánto tiempo funcionó con batería.
10. En **Sesiones recientes** puedes consultar cada periodo de carga y descarga, incluyendo duración, porcentaje inicial y porcentaje final.
11. Los sliders **Límite del procesador (%)** permiten reducir el consumo máximo de CPU por separado con corriente y con batería.
12. El historial guarda también el límite de CPU usado en cada sesión y calcula una descarga estimada en **% de batería por hora**. Necesita varias sesiones para comparar si un límite del 30–40% consume menos que uno más alto.
13. La gráfica de **Tiempo de uso** muestra cuántos minutos permaneció la batería en cada porcentaje. Los picos indican porcentajes donde el cambio fue más lento; los valles indican una caída o carga más rápida. La gráfica se actualiza mientras la sesión está activa y conserva los registros anteriores.
14. Pulsa **Monitor de energía** para abrir una ventana independiente con el resumen, la gráfica y el historial completo.
15. El historial compara el consumo estimado en `%/h` y el límite de CPU utilizado. No representa vatios exactos: Windows no expone el consumo instantáneo del procesador de forma compatible con todos los equipos.
16. En el monitor puedes seleccionar cualquier sesión de carga o descarga. La tabla muestra cada porcentaje, los minutos acumulados en él y si corresponde a carga o descarga; la gráfica usa el porcentaje como eje X y los minutos como eje Y para detectar patrones.
17. La tabla incluye la energía estimada por cada 1% (`capacidad total de la batería / 100`) y la capacidad detectada en Wh. Es una estimación; el consumo real puede variar por pérdidas de carga, desgaste y sensores del fabricante.
18. La gráfica de cada sesión dibuja el tiempo en segundos y la potencia media estimada en W para cada porcentaje. La tabla muestra además los Wh estimados y la potencia media de cada porcentaje.
19. El tiempo de la sesión activa se actualiza aunque un protector de carga mantenga fijo el porcentaje. Si el porcentaje no cambia, la energía calculada por porcentaje será 0 Wh porque no hay una variación medible; la duración seguirá registrándose.
20. La gráfica muestra la escala del eje Y directamente: segundos a la izquierda y watts estimados a la derecha. También muestra el promedio de uso real del procesador medido por Windows. Los watts de batería son una estimación basada en cambios de porcentaje; para medir el consumo total del equipo mientras carga se necesita que el fabricante exponga un sensor de potencia o usar un medidor externo.
21. El monitor incluye dos gráficas separadas por sesión: **Potencia media por porcentaje (W)** y **Energía estimada por porcentaje (Wh)**. Ambas usan el porcentaje como eje X y muestran su escala numérica en el eje Y.
8. Activa **Iniciar automáticamente con Windows** si quieres que la aplicación se ejecute al iniciar sesión.
9. La interfaz sigue automáticamente el modo claro u oscuro configurado en Windows.
10. Al pulsar el botón de minimizar, la ventana desaparece de la barra de tareas y queda en **Iconos ocultos**. Haz doble clic en el icono para abrirla de nuevo.
11. Para cerrar el programa definitivamente, pulsa **Cerrar programa** o haz clic derecho en el icono y elige **Salir**. Cerrar la ventana con la X solamente la deja en la bandeja.

Cuando el equipo está desconectado del cargador, la aplicación reduce el brillo al valor configurado. Al conectar el cargador, restaura el brillo anterior. El control de brillo depende de que el monitor exponga la interfaz WMI de Windows; algunos monitores externos pueden no admitirlo.

Cuando el ahorro automático está activo, la aplicación selecciona en Windows **Ahorro de energía máximo** al usar batería y **Rendimiento máximo** al conectar el cargador. Esto usa los planes de energía integrados de Windows y puede variar ligeramente según la edición o fabricante del equipo.

La configuración se guarda en `%APPDATA%\AutoResolution\settings.json`.
El registro acumulado de tiempos se guarda en `%APPDATA%\AutoResolution\power-usage.json`.

## Usar en otra computadora

La carpeta `publish` contiene una versión autónoma para Windows x64:

- No necesita instalar .NET.
- No necesita conexión a Internet.
- Copia `publish\AutoResolution.exe` a la otra computadora y ejecútalo.

La versión publicada está preparada para Windows de 64 bits. La aplicación necesita permisos normales de usuario; el cambio de resolución lo realiza Windows.

## Compilar

Para publicar incluyendo el runtime de .NET:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
