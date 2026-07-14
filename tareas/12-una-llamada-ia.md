# T12 — Una llamada a la IA por tarea (hoy son dos)

**Estado:** ✅ hecho (2026-07-02, opción A) · **Esfuerzo global:** S · **Depende de:** T9

## El quiebre (por qué)

Cada tarea enviada paga **dos** round-trips secuenciales a Gemini: primero el juicio (dificultad/categoría/plausibilidad) y después el feedback emocional. Eso duplica la latencia del momento más importante de la app —el usuario esperando su recompensa— y duplica el gasto de cuota/costo por tarea sin obtener nada a cambio: el segundo prompt trabaja sobre la misma información que el primero. Además el feedback sale en inglés en una app en español.

## Evidencia en el código

- `src/PetProductivity.Server/Controllers/TasksController.cs:35-45` — `ProcessTaskCompletion` (dentro: `EvaluateTaskAsync` → llamada 1) y luego `GenerateFeedbackAsync` (llamada 2), en serie.
- `src/PetProductivity.Server/Services/AiJudgeService.cs:50-57` — el prompt del juicio ya exige JSON estructurado con `reasoning` (que hoy nadie usa): añadir un campo más es trivial.
- `src/PetProductivity.Server/Services/EmotionalSupportService.cs:37-47` — prompt y fallbacks **en inglés** ("Take a proper break now", "Stay hydrated").
- `src/PetProductivity.Server/Program.cs` (rate limit "ai" 10/min) — la cuota se consume al doble de velocidad de lo necesario.

## Opciones

### A. Fusionar: el juicio devuelve también el feedback (recomendada)
Añadir al JSON del prompt de `AiJudgeService` un campo `feedback` ("una frase estoica pero alentadora, específica a la tarea, EN ESPAÑOL, máx. 2 líneas") y a `AiJudgmentResult` la propiedad. `TasksController` deja de llamar a `EmotionalSupportService.GenerateFeedbackAsync`; el fallback heurístico del juicio incluye frases fijas en español (portar el `switch` por dificultad que ya existe en `EmotionalSupportService.cs:68-74`, traducido).
- **Pros:** ~50% menos latencia percibida y mitad de costo/cuota; el feedback gana contexto (el modelo ya "pensó" la dificultad al escribirlo); de paso se corrige el idioma.
- **Contras:** un prompt que hace dos trabajos puede degradar levemente alguno (mitigable: el juicio es JSON estricto y el feedback es un string libre dentro del JSON — no compiten); `GenerateRevivalEncouragementAsync` (cristalización) queda como única razón de existir de `EmotionalSupportService` — mover o dejar.
- **Esfuerzo:** S · **Toca:** server (2 servicios + controller).

### B. Paralelizar las dos llamadas
`Task.WhenAll` sobre juicio y feedback. Problema: el feedback necesita la dificultad, que sale del juicio → habría que generarlo "a ciegas" (sin dificultad) o mantener la secuencia. 
- **Pros:** casi nada que tocar.
- **Contras:** no reduce costo, el feedback pierde el dato de dificultad, y la latencia solo baja al máximo de las dos. Peor que A en todo salvo esfuerzo.
- **Esfuerzo:** S · Descartable frente a A.

### C. Feedback sin IA (plantillas locales)
Eliminar la segunda llamada y usar solo el `switch` por dificultad/categoría (traducido, con 3-4 variantes aleatorias por rango para que no se repita).
- **Pros:** latencia y costo mínimos absolutos; determinista.
- **Contras:** pierde la especificidad ("acknowledges THIS task") que es justo lo que hace sentir *visto* al usuario — el feedback contextual es de las pocas cosas que diferencian esta app de un todo-list.
- **Esfuerzo:** S · Válida solo como fallback (que es exactamente lo que A ya contempla).

## Recomendación

**A.** B no aporta y C sacrifica el valor diferencial. Al implementarla, verificar el log existente ("Gemini Raw Response") para confirmar que el JSON llega bien formado con el campo nuevo, y dejar `GenerateRevivalEncouragementAsync` donde está (se usa en otro flujo y es 1 llamada legítima).

## Criterios de éxito / verificación

1. Enviar una tarea genera **una** llamada a Gemini (verificable en logs: un solo "Generated Prompt").
2. La respuesta incluye feedback específico a la tarea, en español.
3. Con Gemini caído (apagar la key en dev), la tarea se premia igual con el fallback heurístico Y un feedback en español — sin excepción.
4. Latencia de `POST /api/tasks` medida antes/después (esperable ~mitad).
5. Test xUnit del parseo de `AiJudgmentResult` con el campo nuevo presente y ausente (respuesta vieja no debe romper).

## Dependencias

- **T9** primero. Sinergia con **T16** (unificación de idioma): esta tarea arregla el caso más visible (feedback en inglés).

## Resultado (2026-07-02) — opción A implementada y verificada en vivo

- Prompt del juez con regla 6 + campo JSON `feedback` (español, estoico, específico); `AiJudgmentResult.Feedback`; `EvaluateTaskAsync` devuelve 5-tupla; `TaskResult.EmotionalFeedback` (la clave `emotionalFeedback` de la respuesta no cambia → cliente intacto). `TasksController` ya no llama a `EmotionalSupportService`.
- Fallback local en español (`FallbackFeedback`, switch por dificultad portado y traducido) cubre: IA caída, JSON viejo sin el campo, o campo vacío.
- **Criterios:** (1) ✓ logs: 2 tareas → 2 "Generated Prompt", 0 "Generating emotional support". (2) ✓ feedback real de Gemini en español y específico ("El trabajo constante en matemáticas fatiga la capacidad cognitiva; …"). (3) ✓ con `Gemini__ApiKey` inválida: 200, premio aplicado y fallback en español, sin excepción. (4) estructural: 2 llamadas→1 (latencias medidas 3.4–14.4 s, dominadas por Gemini). (5) ✓ tests de parseo con/sin campo + fallback (suite 34 verdes).
- Efecto colateral esperado: `EmotionalSupportService` queda **sin llamadores** (ni `GenerateFeedbackAsync` ni `GenerateRevivalEncouragementAsync` tenían otros usos) → borrarlo entero en **T18-7** (junto a su registro en DI).
