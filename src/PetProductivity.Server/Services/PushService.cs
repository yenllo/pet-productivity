using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;

namespace PetProductivity.Server.Services;

// Envía notificaciones push (FCM) con la app cerrada. Si Firebase no está configurado, es un no-op silencioso.
public class PushService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushService> _logger;
    private static FirebaseApp? _app;
    private static readonly object _lock = new();

    public PushService(AppDbContext db, IConfiguration config, ILogger<PushService> logger)
    {
        _db = db;
        _logger = logger;
        EnsureInit(config, logger);
    }

    private static void EnsureInit(IConfiguration config, ILogger logger)
    {
        if (_app != null) return;
        lock (_lock)
        {
            if (_app != null) return;
            if (FirebaseApp.DefaultInstance != null) { _app = FirebaseApp.DefaultInstance; return; }
            try
            {
                GoogleCredential? cred = null;
                var json = config["Firebase:ServiceAccountJson"];
                var path = config["Firebase:ServiceAccountPath"];
                if (!string.IsNullOrWhiteSpace(json)) cred = GoogleCredential.FromJson(json);
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) cred = GoogleCredential.FromFile(path);

                if (cred == null) { logger.LogWarning("Firebase sin credenciales: push deshabilitado."); return; }
                _app = FirebaseApp.Create(new AppOptions { Credential = cred, ProjectId = config["Firebase:ProjectId"] });
                logger.LogInformation("Firebase push inicializado.");
            }
            catch (Exception ex) { logger.LogError(ex, "Fallo inicializando Firebase; push deshabilitado."); }
        }
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body)
    {
        if (_app == null) return; // push deshabilitado (sin credenciales)

        var ids = userIds.ToList();
        var users = await _db.Users
            .Where(u => ids.Contains(u.Id) && u.DeviceToken != null && u.NotificationsEnabled)
            .Select(u => new { u.Id, Token = u.DeviceToken! })
            .ToListAsync();

        _logger.LogInformation("Push: enviando a {Count} token(s): {Title}", users.Count, title);
        var deadUserIds = new List<Guid>();
        foreach (var u in users)
        {
            try
            {
                var id = await FirebaseMessaging.DefaultInstance.SendAsync(new Message
                {
                    Token = u.Token,
                    Notification = new Notification { Title = title, Body = body },
                    // Prioridad alta: MIUI/Xiaomi entrega mejor con app en segundo plano.
                    Android = new AndroidConfig { Priority = Priority.High }
                });
                _logger.LogInformation("Push enviado OK, messageId={Id}", id);
            }
            // Unregistered = FCM confirma que el token ya no existe (reinstaló, cambió de teléfono,
            // desinstaló). Antes esto se logueaba como advertencia genérica y se reintentaba PARA
            // SIEMPRE en cada notificación futura. El cliente vuelve a registrar su token vigente en
            // cada apertura (PushRegistration.RegisterAsync), así que limpiar aquí es autosanable: no
            // hay forma de perder al usuario, solo se evita seguir insistiendo contra un token muerto.
            catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                _logger.LogInformation("Token de {UserId} desregistrado en FCM; se limpia.", u.Id);
                deadUserIds.Add(u.Id);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Push falló para un token (fallo transitorio, se reintenta después)."); }
        }

        if (deadUserIds.Count > 0)
        {
            var deadUsers = await _db.Users.Where(u => deadUserIds.Contains(u.Id)).ToListAsync();
            foreach (var u in deadUsers) u.DeviceToken = null;
            await _db.SaveChangesAsync();
        }
    }
}
