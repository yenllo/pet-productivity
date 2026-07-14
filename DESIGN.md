> ⚠️ **DOCUMENTO HISTÓRICO — NO REFLEJA EL ESTADO ACTUAL.**
> Describe la arquitectura original (IA local con Ollama, "Home Base" PC↔teléfono, SQLite).
> El proyecto la superó: hoy es **backend en la nube (Render) + IA Gemini + PostgreSQL/Supabase + JWT**.
> La fuente de verdad es **`CLAUDE.md`** (y `ROADMAP.md` para fases). Conservado solo como referencia.

# 📘 Especificación Técnica: Proyecto "PetProductivity"
**Objetivo:** Gamificar el trabajo duro ("Hard Work") mediante mascotas evolutivas, economía dual y mecánicas sociales, potenciado por IA Local (Ollama) para mantener la privacidad y el rigor.

## 1. El Núcleo del Juego (The Core Loop)
El sistema evita la inflación de XP usando a la IA como juez imparcial.

1.  **Input:** Usuario ingresa tarea (ej: "Resolver 10 integrales").
2.  **Juicio (IA Local):**
    * El modelo analiza semánticamente el texto.
    * Determina **Categoría** (¿Es Lógica? ¿Es Fuerza?) y **Dificultad** (1-10).
3.  **Recompensa Dual:**
    * 🧬 **Stats (Evolución):** La tarea alimenta un atributo específico (ej. +5 Lógica). La mascota cambia físicamente según qué Stats suben más (ej. le crecen gafas o músculos).
    * 💰 **Oro (Cosmética):** Moneda universal por completar tareas. Se usa para comprar *Skins* y accesorios (ropa, fondos) que no afectan las stats.

---

## 2. Arquitectura "Home Base" (Solución Móvil)
Para permitir que teléfonos de gama baja usen IA potente sin quemar su batería:

### A. El Cerebro (Tu PC Fedora)
*   **Rol:** Servidor Local de Inteligencia.
*   **Software:** Ollama corriendo Qwen 32b (o 7b).
*   **Función:** Recibe textos, los procesa y devuelve el resultado JSON.

### B. El Cuerpo (App Móvil .NET MAUI)
*   **Rol:** Interfaz de Usuario y Mascota.
*   **Requisitos:** Mínimos. No corre IA, solo gráficos 2D y peticiones HTTP.
*   **Modo Desconectado (Offline):**
    *   Si el usuario está fuera de casa (sin acceso al PC), las tareas se guardan en una **Cola Local**.
    *   Al llegar a casa (Wi-Fi Local), la app sincroniza con el PC y procesa todas las tareas pendientes de golpe.

---

## 3. Sistema de Arquetipos (Focos)
El usuario o grupo elige un "Foco" que define qué barras de progreso existen.

### A. Arquetipos Individuales (Base)
* 🎓 **El Erudito:** Lógica, Memoria, Elocuencia.
* 💻 **El Tecnólogo:** Código, Arquitectura, Debugging.
* 🎨 **El Visionario:** Creatividad, Técnica, Estética.
* ⚡ **El Atleta:** Fuerza, Resistencia, Disciplina.
* 💼 **El Estratega:** Finanzas, Networking, Gestión.

### B. Arquetipos Grupales (Exclusivos Multi-Usuario)
* 🏠 **El Hogar:** Mantenimiento, Cuidado, Administración.
    * *Uso:* Parejas, compañeros de piso.
* 🤝 **El Gremio:** Avance, Soporte, Calidad.
    * *Uso:* Proyectos colaborativos, Hackathons.

---

## 4. Mecánicas Sociales y de Sincronización
Diseñadas para coordinar el trabajo sin ser invasivas.

### A. El Semáforo de Disponibilidad (Status System)
Evita el spam. Las notificaciones de grupo dependen del estado del usuario:
1.  🟢 **Disponible (LFG):** "Quiero trabajar. Avísame si alguien empieza." (Recibe invites).
2.  🔨 **Trabajando:** "Estoy ocupado sumando puntos." (Detona notificaciones a los 'Disponibles').
3.  🔴 **Ocupado:** "No molestar." (Ignorado por el sistema).
4.  ⚫ **Inactivo:** Offline.

### B. El Bono de Sinergia (Combo)
* **Condición:** 2+ usuarios en estado "Trabajando" completan tareas dentro de una ventana de tiempo (ej. 15 min).
* **Efecto:** Se activa modo "Frenesí" (Multiplicador de XP/Oro x1.5 o x2) por 1-2 horas para el grupo.

### C. Mascota Compartida & Anti-Polizón
* La salud de la mascota es compartida.
* Si un usuario no aporta, la mascota reacciona individualmente (se muestra huraña o triste solo con ese usuario) para generar presión social positiva.

---

## 5. Roles de la Inteligencia Artificial (Backend)
Implementado en C# (.NET 8) conectando con Ollama (Fedora).

1.  **⚖️ El Tasador (The Appraiser):**
    * *Función:* Input Tarea -> Output JSON `{ "difficulty": 7, "category": "Logic" }`.
2.  **🛡️ El Compañero (The Support):**
    * *Función:* Genera feedback emocional contextual post-tarea.
    * *Ejemplo:* "Esa sesión de código fue intensa. Tus ojos necesitan descanso, ve por agua."
3.  **📡 El Coordinador (The Matchmaker):**
    * *Función:* Detecta oportunidades de Sincronización entre usuarios "Disponibles" y sugiere unirse.

---

### 🛠️ Stack Tecnológico Local
* **OS:** Fedora 43 (Servidor/Cerebro).
* **App Móvil:** .NET 8 MAUI (Android/iOS).
* **Comunicación:** HTTP/REST sobre Wi-Fi Local (o Tailscale para acceso remoto seguro).
* **Base de Datos:** SQLite (Sincronizada).
