using System.Globalization;

namespace PetProductivity.Client.Services;

// T27-L3 (#26): localización ES/EN. La clave ES el string en español tal como está en el código:
// si falta una traducción, la UI cae al español (visible y no rompe). Idioma: Preferences["AppLang"]
// = "system" | "es" | "en"; "system" sigue el idioma del teléfono. Cambiarlo reconstruye el Shell
// (mismo truco que el tema #25), así que L.T se re-evalúa al re-inflar las páginas.
public static class L
{
    public static string Lang { get; private set; } = "es";

    public static void Init()
    {
        var pref = Preferences.Get("AppLang", "system");
        Lang = pref == "system"
            ? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en")
            : pref;
    }

    public static string T(string es) =>
        Lang == "en" && En.TryGetValue(es, out var v) ? v : es;

    public static string F(string es, params object[] args) => string.Format(T(es), args);

    private static readonly Dictionary<string, string> En = new()
    {
        // ---- Navegación / pestañas ----
        ["Mascota"] = "Pet",
        ["Familias"] = "Families",
        ["Tienda"] = "Shop",
        ["Perfil"] = "Profile",
        ["Ajustes"] = "Settings",

        // ---- Onboarding / auth ----
        ["Toca para continuar"] = "Tap to continue",
        ["¿Cómo se llamará tu mascota?"] = "What will your pet be called?",
        ["Nombre de la mascota"] = "Pet name",
        ["Comenzar"] = "Start",
        ["¿Ya tienes cuenta? Inicia Sesión"] = "Already have an account? Log in",
        // Nombre por defecto de la mascota al registrarse directo (sin pasar por la ceremonia).
        ["Mascota de {0}"] = "{0}'s Pet",
        ["¿No tienes cuenta? Regístrate"] = "No account? Sign up",
        ["Bienvenido de vuelta"] = "Welcome back",
        ["Únete a la revolución productiva"] = "Join the productivity revolution",
        ["Iniciar sesión"] = "Log in",
        ["Iniciar sesión o registrarse"] = "Log in or sign up",
        ["Registrarse"] = "Sign up",
        ["Crear cuenta"] = "Create account",
        ["Continuar con Google"] = "Continue with Google",
        ["Correo electrónico"] = "Email",
        ["Contraseña"] = "Password",
        ["Confirmar contraseña"] = "Confirm password",
        ["Nombre completo"] = "Full name",
        ["Nombre"] = "Name",
        ["Cerrar sesión"] = "Log out",
        ["Guarda tu progreso en la nube y juega en cualquier dispositivo."] =
            "Save your progress to the cloud and play on any device.",
        ["Credenciales inválidas."] = "Invalid credentials.",
        ["Las contraseñas no coinciden."] = "Passwords don't match.",
        ["No se pudo registrar. El correo podría estar en uso."] = "Couldn't sign up. That email may already be in use.",
        ["Por favor completa todos los campos."] = "Please fill in all fields.",
        ["Por favor ingresa correo y contraseña."] = "Please enter email and password.",
        ["No se pudo iniciar sesión con Google."] = "Couldn't sign in with Google.",
        ["Sesión inválida."] = "Invalid session.",
        ["Error"] = "Error",
        ["Error de navegación"] = "Navigation error",
        ["No se pudo navegar: {0}"] = "Couldn't navigate: {0}",

        // ---- Dashboard / mascota ----
        ["Registrar progreso"] = "Log progress",
        ["Evolución"] = "Evolution",
        ["Cría"] = "Baby",
        ["Adulto"] = "Adult",
        ["Maestro"] = "Master",
        ["Hambre"] = "Hunger",
        ["Salud"] = "Health",
        ["Crecimiento"] = "Growth",
        ["Cuerpo"] = "Body",
        ["Mente"] = "Mind",
        ["Hogar"] = "Home",
        ["Bienestar"] = "Wellness",
        ["Ritual diario"] = "Daily ritual",
        ["Toca una celda para nombrar ese hábito."] = "Tap a cell to name that habit.",
        ["Nombra este hábito"] = "Name this habit",
        ["Listo"] = "Done",
        ["Guardar"] = "Save",
        ["Cancelar"] = "Cancel",
        ["Enviar"] = "Send",
        ["Tu estado"] = "Your status",
        ["🟢 Disponible"] = "🟢 Available",
        ["🔨 Trabajando"] = "🔨 Working",
        ["🔴 Ocupado"] = "🔴 Busy",
        ["⚫ Desconectado"] = "⚫ Offline",
        ["Desconectado"] = "Offline",
        ["Disponible"] = "Available",
        ["Trabajando"] = "Working",
        ["Ocupado"] = "Busy",
        ["🔥 ¡FRENESÍ! ×2 XP — ¡varios trabajando!"] = "🔥 FRENZY! ×2 XP — several working!",
        ["Estado cristalizado"] = "Crystallized state",
        ["Tu mascota se cristalizó. Rompe el cristal con esfuerzo real (un foco o una tarea 5+) durante 3 días distintos, o con una hazaña épica (9+)."] =
            "Your pet crystallized. Crack the crystal with real effort (a focus session or a 5+ task) on 3 different days, or with an epic feat (9+).",
        ["🔥 Iniciar misión de resurrección"] = "🔥 Start resurrection mission",
        ["💠 Grietas del cristal: {0}/3"] = "💠 Crystal cracks: {0}/3",
        ["Toca un mueble para seleccionarlo; luego toca una celda para moverlo."] =
            "Tap a furniture piece to select it, then tap a cell to move it.",
        ["Rotar ⟳"] = "Rotate ⟳",
        ["Quitar ✕"] = "Remove ✕",
        ["✕  Cancelar"] = "✕  Cancel",

        // ---- Sandbox de edición del cuarto ----
        ["Editar cuarto"] = "Edit room",
        ["✕ Cancelar"] = "✕ Cancel",
        ["✓ Guardar"] = "✓ Save",
        ["✓ Listo"] = "✓ Done",
        ["✥ Mover"] = "✥ Move",
        ["🗄️ Guardar en inventario"] = "🗄️ Store in inventory",
        ["🗄️ Guardados ({0})"] = "🗄️ Stored ({0})",
        ["🛋️ En el cuarto ({0})"] = "🛋️ In the room ({0})",
        ["Poner ↴"] = "Place ↴",
        ["Toca un objeto del cuarto o de las listas."] = "Tap an object in the room or in the lists.",
        ["Todo lo tuyo está en el cuarto. Lo que guardes aparecerá aquí."] =
            "Everything you own is in the room. Whatever you store will show up here.",
        ["Cuarto guardado ✓"] = "Room saved ✓",
        ["No se pudo conectar. Espera unos segundos y vuelve a intentar."] =
            "Couldn't connect. Wait a few seconds and try again.",
        ["No hay espacio: mueve o quita algo primero."] = "No space: move or store something first.",
        ["✓ {0} colocado en tu cuarto"] = "✓ {0} placed in your room",
        ["✓ Comprado. Cuarto lleno: {0} quedó en Guardados (lápiz ✏️ del cuarto)"] =
            "✓ Purchased. Room full: {0} went to Stored (room pencil ✏️)",
        ["Repetir de un toque"] = "One-tap repeat",
        ["Repetir hábito"] = "Repeat habit",
        ["Registrar igual"] = "Log anyway",
        ["Fuera de contexto"] = "Out of context",
        ["¡Evolución!"] = "Evolution!",

        // ---- Tarea / recompensa ----
        ["¿Qué lograste hoy?"] = "What did you achieve today?",
        ["Ej: Estudié 2 horas de Cálculo Diferencial..."] = "E.g.: I studied Calculus for 2 hours...",
        ["Reclamar recompensa"] = "Claim reward",
        ["⏱ Modo foco"] = "⏱ Focus mode",
        ["Modo foco"] = "Focus mode",
        ["Analizando dificultad..."] = "Analyzing difficulty...",
        ["La IA está juzgando tu esfuerzo..."] = "The AI is judging your effort...",
        ["Aún no has registrado tareas hoy"] = "No tasks logged today yet",
        ["¡Completa tu primera tarea para ganar XP!"] = "Complete your first task to earn XP!",
        ["¡Recompensa!"] = "Reward!",
        ["¡Genial!"] = "Awesome!",
        [" (reducido)"] = " (reduced)",
        ["+{0} XP"] = "+{0} XP",
        ["+{0} Oro"] = "+{0} Gold",
        ["+{0} XP · +{1} Oro"] = "+{0} XP · +{1} Gold",
        ["🔥 ¡RENACIMIENTO! 🔥"] = "🔥 REBIRTH! 🔥",
        ["⚠️ CRISTALIZADO ⚠️"] = "⚠️ CRYSTALLIZED ⚠️",
        ["Error técnico: {0}"] = "Technical error: {0}",
        ["Error de conexión con el servidor. No se pudo evaluar la tarea."] =
            "Server connection error. The task couldn't be evaluated.",
        ["Escribe qué vas a hacer antes de iniciar el foco."] = "Write what you're going to do before starting the focus.",
        ["📡 Sin conexión: tu tarea quedó guardada y se enviará sola al reconectar."] =
            "📡 Offline: your task was saved and will be sent automatically when you reconnect.",
        ["📡 Sin conexión: tu foco quedó guardado y se enviará solo al reconectar."] =
            "📡 Offline: your focus was saved and will be sent automatically when you reconnect.",
        ["📤 Enviado"] = "📤 Sent",
        ["\"{0}\" llegó al servidor.{1}"] = "\"{0}\" reached the server.{1}",

        // ---- Foco ----
        ["Comenzar foco"] = "Start focus",
        ["Cancelar foco (sin recompensa)"] = "Cancel focus (no reward)",
        ["Duración del foco"] = "Focus length",
        ["Quédate en PetProductivity o en tus apps permitidas."] = "Stay in PetProductivity or your allowed apps.",
        ["🔒 Tienes un foco activo"] = "🔒 You have an active focus",
        ["Volver ›"] = "Back ›",
        ["Comprobante en revisión…"] = "Proof under review…",
        ["Tus focos"] = "Your focus sessions",
        ["Aún no hay focos en tu historial"] = "No focus sessions in your history yet",
        ["Foco cancelado. Sin recompensa."] = "Focus canceled. No reward.",
        ["Foco grupal en marcha."] = "Group focus underway.",
        ["El foco grupal ya terminó."] = "The group focus already ended.",
        ["No hay una tarea para enfocar."] = "There's no task to focus on.",
        ["No se pudo iniciar el foco."] = "Couldn't start the focus.",
        ["No se pudo iniciar el foco grupal."] = "Couldn't start the group focus.",
        ["No se pudo unir al foco grupal."] = "Couldn't join the group focus.",
        ["No se pudo unir al foco grupal: {0}"] = "Couldn't join the group focus: {0}",
        ["Ya hay un foco activo."] = "There's already an active focus.",
        ["Cámara no disponible."] = "Camera not available.",
        ["Sin permiso de cámara."] = "No camera permission.",
        ["Comprobante: {0}"] = "Proof: {0}",
        ["Historial"] = "History",
        ["Estas apps podrás abrir durante el foco (máx 3). El resto queda bloqueado."] =
            "You can open these apps during focus (max 3). Everything else is blocked.",
        ["No hay apps disponibles."] = "No apps available.",

        // ---- Familias / grupos ----
        ["Comunidad"] = "Community",
        ["Mis familias"] = "My families",
        ["Aún no estás en ninguna familia"] = "You're not in any family yet",
        ["Crea una o únete con un código."] = "Create one or join with a code.",
        ["+ Crear familia"] = "+ Create family",
        ["Crear familia"] = "Create family",
        ["Nueva familia"] = "New family",
        ["Unirse por código"] = "Join with a code",
        ["Ej: 3GKD7L"] = "E.g.: 3GKD7L",
        ["Ej: Gremio de Estudio"] = "E.g.: Study Guild",
        ["Ej: Gym"] = "E.g.: Gym",
        ["Contexto (arquetipo)"] = "Context (archetype)",
        // Opciones del picker de arquetipo en "Nueva familia" (CreateGroupViewModel.Archetypes):
        // vivían como strings sueltos sin pasar por L.T, así que con el idioma en inglés se veía
        // "Estudio" en medio de una pantalla en inglés (encontrado probando en el emulador).
        ["Estudio"] = "Study",
        ["Tecnología"] = "Technology",
        ["Creativo"] = "Creative",
        ["Atlético"] = "Athletic",
        ["Ejecutivo"] = "Executive",
        ["Hogar (pareja)"] = "Home (couple)",
        ["Gremio"] = "Guild",
        ["Miembros"] = "Members",
        ["Código"] = "Code",
        ["Solicitudes pendientes"] = "Pending requests",
        ["Tareas por validar"] = "Tasks to validate",
        ["Aprobar"] = "Approve",
        ["Salir de la familia"] = "Leave family",
        ["Salir"] = "Leave",
        ["¿Salir de {0}?"] = "Leave {0}?",
        ["Registrar tarea a esta mascota"] = "Log a task for this pet",
        ["🥚 Hacer nacer"] = "🥚 Hatch",
        ["Esperando a que todos lo hagan nacer…"] = "Waiting for everyone to hatch it…",
        ["El huevo necesita que todos toquen «Hacer nacer»."] = "The egg needs everyone to tap “Hatch”.",
        ["La mascota debe estar despierta (mín. 2 miembros)."] = "The pet must be awake (min. 2 members).",
        ["La mascota necesita al menos 2 miembros para recibir tareas."] =
            "The pet needs at least 2 members to receive tasks.",
        ["No se pudo cargar la familia. Revisa tu conexión."] = "Couldn't load the family. Check your connection.",
        ["Falta el nombre"] = "Missing name",
        ["Ponle un nombre a tu familia."] = "Give your family a name.",
        ["Tu familia se activó"] = "Your family is now active",
        ["📷 Historial del grupo"] = "📷 Group history",
        ["por {0}"] = "by {0}",
        ["⏳ se aprueba sola en {0} h"] = "⏳ auto-approves in {0} h",
        ["{0} días"] = "{0} days",
        ["Máximo de miembros: {0:0}"] = "Max members: {0:0}",
        ["{0} quiere unirse"] = "{0} wants to join",

        // ---- Tienda ----
        ["Mercader"] = "Merchant",
        ["Buscar objetos…"] = "Search items…",
        ["Todo"] = "All",
        ["La tienda está vacía por ahora."] = "The shop is empty for now.",
        ["No se pudo cargar la tienda. Revisa tu conexión."] = "Couldn't load the shop. Check your connection.",
        ["No se encontraron objetos."] = "No items found.",
        ["Página {0} de {1}"] = "Page {0} of {1}",
        ["‹ Anterior"] = "‹ Previous",
        ["Siguiente ›"] = "Next ›",
        ["Equipado ✓"] = "Equipped ✓",
        ["Equipar"] = "Equip",
        ["Comprado"] = "Owned",
        ["Terminado"] = "Ended",
        ["⏳ termina en {0}d"] = "⏳ ends in {0}d",
        ["⏳ termina en {0}h"] = "⏳ ends in {0}h",
        ["⏳ termina en {0}m"] = "⏳ ends in {0}m",
        ["✓ Estilo equipado"] = "✓ Style equipped",
        ["✓ Has comprado {0}"] = "✓ You bought {0}",
        ["✓ {0} desbloqueado"] = "✓ {0} unlocked",

        // ---- Perfil / stats ----
        ["Invitado"] = "Guest",
        ["Cuidador"] = "Caretaker",
        ["Atributos"] = "Attributes",
        ["Etapa"] = "Stage",
        ["Etapa actual"] = "Current stage",
        ["{0} XP totales"] = "{0} total XP",
        ["🔥 Racha"] = "🔥 Streak",
        ["🔥 {0} racha"] = "🔥 {0} streak",
        ["✅ Tareas"] = "✅ Tasks",
        ["✅ {0} tareas"] = "✅ {0} tasks",
        ["🎯 {0} días de foco · {1} min"] = "🎯 {0} focus days · {1} min",
        ["📷 Ver historial de tareas"] = "📷 View task history",
        ["Estadísticas"] = "Statistics",
        ["Rendimiento global"] = "Overall performance",

        // ---- Ajustes ----
        ["Preferencias"] = "Preferences",
        ["Configuración"] = "Settings",
        ["Apariencia"] = "Appearance",
        ["Modo oscuro"] = "Dark mode",
        ["Idioma"] = "Language",
        ["Sistema"] = "System",
        ["Notificaciones"] = "Notifications",
        ["Activar notificaciones"] = "Enable notifications",
        ["Recordar el ritual diario"] = "Daily ritual reminder",
        ["Acceso de uso"] = "Usage access",
        ["Mostrar sobre otras apps"] = "Display over other apps",
        ["Conceder"] = "Grant",
        ["Elegir"] = "Choose",
        ["Apps permitidas"] = "Allowed apps",
        ["Meta diaria (min)"] = "Daily goal (min)",
        ["Recompensas por foto"] = "Photo rewards",
        ["Pide una foto a mitad del foco; verificada da 2× la recompensa. Opcional."] =
            "Asks for a photo mid-focus; if verified, doubles the reward. Optional.",
        ["Durante el foco solo podrás usar PetProductivity y las apps permitidas (máx 3)."] =
            "During focus you can only use PetProductivity and your allowed apps (max 3).",
        ["Acerca de"] = "About",
        ["Versión"] = "Version",
        ["Créditos"] = "Credits",
        ["Muebles del cuarto: Bongseng (bongseng.itch.io).\n\nHecho con .NET MAUI, ASP.NET Core y Google Gemini."] =
            "Room furniture: Bongseng (bongseng.itch.io).\n\nBuilt with .NET MAUI, ASP.NET Core, and Google Gemini.",
        ["Desarrollador"] = "Developer",
        ["Opciones de desarrollador"] = "Developer options",
        ["URL del servidor"] = "Server URL",
        ["Modelo de IA"] = "AI model",
        ["Probar ping"] = "Test ping",
        ["Requiere Reinicio"] = "Requires Restart",
        ["Cambiar estos valores requiere reiniciar la app."] = "Changing these values requires restarting the app.",
        ["Notificaciones Activadas"] = "Notifications enabled",
        ["Recibirás alertas sobre tu mascota."] = "You'll get alerts about your pet.",
        ["Falta"] = "Missing",
        ["{0}/3 apps permitidas"] = "{0}/3 allowed apps",
        ["‹ Atrás"] = "‹ Back",

        // ---- T14-C1: cuenta, privacidad y borrado ----
        ["Cuenta"] = "Account",
        ["Política de privacidad"] = "Privacy policy",
        ["Borrar cuenta"] = "Delete account",
        ["Se eliminarán tu cuenta, tu mascota, tu historial y tus fotos de forma permanente. Si eres el último miembro de un grupo, el grupo también se borra. Esta acción no se puede deshacer."] =
            "Your account, pet, history and photos will be permanently deleted. If you are the last member of a group, the group is deleted too. This cannot be undone.",
        ["Continuar"] = "Continue",
        ["Confirmación final"] = "Final confirmation",
        ["Escribe {0} para confirmar."] = "Type {0} to confirm.",
        ["BORRAR"] = "DELETE",
        ["No se pudo borrar la cuenta. Revisa tu conexión e inténtalo de nuevo."] =
            "Could not delete the account. Check your connection and try again.",
        ["Foto de comprobante"] = "Proof photo",
        ["La foto se envía a Google Gemini para verificarla con IA y se guarda 30 días en el servidor (después se borra sola). Es siempre opcional: saltarla no penaliza."] =
            "The photo is sent to Google Gemini for AI verification and stored on the server for 30 days (then auto-deleted). It's always optional: skipping it carries no penalty.",
        ["Entendido"] = "Got it",

        // ---- Mensajes generados en C# (segunda pasada) ----
        ["No se pudo"] = "Failed",
        ["{0} empezó a trabajar 🔨 ¡Únete para un Frenesí!"] = "{0} started working 🔨 Join for a Frenzy!",
        ["Racha diaria: {0} día(s) seguidos haciendo algo. El punto naranja avisa que HOY aún no registras nada — ¡haz una tarea o un foco para mantenerla!"] =
            "Daily streak: {0} day(s) in a row doing something. The orange dot warns that TODAY you haven't logged anything yet — do a task or a focus to keep it!",
        ["Racha diaria: {0} día(s) seguidos haciendo algo. ¡Hoy ya está asegurada! ✔"] =
            "Daily streak: {0} day(s) in a row doing something. Today is already secured! ✔",
        ["¡{0} evolucionó a {1}!"] = "{0} evolved into {1}!",
        ["Regístrate o inicia sesión para no perder a {0}"] = "Sign up or log in so you don't lose {0}",
        ["Registrar"] = "Log it",
        ["¿Registrar de nuevo?"] = "Log it again?",
        ["Huevo"] = "Egg",
        ["Únete al foco grupal"] = "Join the group focus",
        ["🎯 Iniciar foco grupal"] = "🎯 Start group focus",
        ["Foco grupal"] = "Group focus",
        ["¿En qué van a trabajar juntos?"] = "What will you work on together?",
        ["Dormida — comparte el código (mín. 2 miembros)"] = "Asleep — share the code (min. 2 members)",
        ["Activa"] = "Active",
        ["Huevo — comparte el código (mín. 2 miembros)"] = "Egg — share the code (min. 2 members)",
        ["Huevo — todos deben tocar «Hacer nacer»"] = "Egg — everyone must tap “Hatch”",
        ["Feliz"] = "Happy",
        ["Huraño"] = "Grumpy",
        ["Ahora no"] = "Not now",
        ["📸 Tomar selfie"] = "📸 Take a selfie",
        ["🖼️ Subir foto"] = "🖼️ Upload a photo",
        ["Demuestra que estás siendo productivo y gana {0} las recompensas: una selfie o una foto de lo que haces. (Desactívalo en Ajustes → Recompensas por foto)"] =
            "Prove you're being productive and earn {0} the rewards: a selfie or a photo of what you're doing. (Turn it off in Settings → Photo rewards)",
        ["Comprobante enviado."] = "Proof sent.",
        ["¡Comprobante verificado! {0} recompensa al terminar."] = "Proof verified! {0} reward when you finish.",
        ["No se reconoció la foto; recompensa normal."] = "The photo couldn't be verified; normal reward.",
        ["¿Cancelar el foco?"] = "Cancel the focus?",
        ["Si aguantas hasta el final ganarás ~{0} XP y ~{1} Oro."] = "If you hold on until the end you'll earn ~{0} XP and ~{1} Gold.",
        ["Si cancelas ahora no recibes nada."] = "If you cancel now you get nothing.",
        ["Sí, cancelar"] = "Yes, cancel",
        ["Seguir concentrado"] = "Stay focused",
        ["Sin apps permitidas (solo PetProductivity). Elige hasta 3 en Ajustes → Modo foco."] =
            "No allowed apps (PetProductivity only). Pick up to 3 in Settings → Focus mode.",
        ["{0} app(s) permitida(s). Cámbialas en Ajustes → Modo foco."] = "{0} allowed app(s). Change them in Settings → Focus mode.",
        ["En foco. Quédate en PetProductivity o en tus apps permitidas."] = "In focus. Stay in PetProductivity or your allowed apps.",
        ["En foco. Mantén la app abierta."] = "In focus. Keep the app open.",
        ["📸 Comprobante disponible"] = "📸 Proof available",
        ["Vuelve a PetProductivity y demuestra tu foco para ganar 2× la recompensa."] =
            "Come back to PetProductivity and prove your focus to earn 2× the reward.",
        ["¡Foco completado! +{0} XP y +{1} Oro."] = "Focus complete! +{0} XP and +{1} Gold.",
        ["¡Familia creada!"] = "Family created!",
        ["¡Nuevo miembro aceptado!"] = "New member accepted!",
        ["Error de conexión. Inténtalo de nuevo."] = "Connection error. Try again.",
        ["Error de conexión."] = "Connection error.",
        [" +{0} XP · +{1} Oro"] = " +{0} XP · +{1} Gold",
        ["Concedido ✅"] = "Granted ✅",
        ["Falta ❌"] = "Missing ❌",
        ["Probando..."] = "Testing...",
        ["Conectado ✅"] = "Connected ✅",
        ["Fallo ❌"] = "Failed ❌",
        ["Sin probar"] = "Untested",

        // ---- Categorías de tienda (solo display; el catálogo sigue en ES) ----
        ["Muebles"] = "Furniture",
        ["Decoración"] = "Decoration",
        ["Vida"] = "Life",
        ["Estructural"] = "Structural",
        ["Estilos"] = "Styles",
        ["Consumibles"] = "Consumables",
        ["Cosmético"] = "Cosmetics",
        ["Eventos"] = "Events",
    };
}
