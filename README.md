🎯 Objetivo Principal
Construir una API RESTful completa aplicando Clean Architecture, principios SOLID y servicios de Azure, implementando el mismo sistema en dos arquitecturas diferentes para comprender sus trade-offs:

ASP.NET Core Web API (tradicional, servidor always-on)
Azure Functions (serverless, event-driven)

¿Por qué simular Salesforce?

Plataforma compleja y bien documentada como modelo de referencia
Permite practicar integración cloud sin dependencias externas
Facilita entender arquitecturas empresariales reales


📚 Conocimientos Clave Practicados
1. Clean Architecture
   Separación estricta en capas con dependencias unidireccionales:
   Presentation (Controllers/Functions)
   ↓ depende de
   Application (Services, DTOs)
   ↓ depende de
   Domain (Entities, Interfaces) ← Núcleo puro, sin dependencias
   ↑ implementado por
   Infrastructure (Repositories, External Services)
   Resultado medible: Migrar de ASP.NET Core a Azure Functions tomó 2 horas porque el Domain Layer se reutilizó 100% sin cambios.

2. Principios SOLID en Acción
   Aplicados consistentemente en ~3,000 líneas de código:
   Single Responsibility
   csharppublic class AccountService { }      // Solo lógica de negocio
   public class AccountRepository { }   // Solo acceso a datos
   public class AccountsController { }  // Solo manejo HTTP
   Dependency Inversion
   csharp// Dependo de abstracciones, no de implementaciones concretas
   public class AccountService
   {
   private readonly IAccountRepository _repository; // Interfaz

   // Funciona con InMemory, SQL, Cosmos - cualquier implementación
   }
   Impacto real: Cambiar de in-memory a base de datos requiere modificar solo 3 archivos (repository implementations) en lugar de 20+ esparcidos por la aplicación.

3. Azure Cloud Technologies
   Azure Functions (Serverless)
   csharp[Function("CreateAccount")]
   public async Task<IActionResult> CreateAccount(
   [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
   {
   // Azure maneja: scaling, load balancing, monitoring
   // Yo solo escribo la lógica de negocio
   }
   Cuándo usar cada arquitectura:
   EscenarioAzure FunctionsASP.NET Core APITráfico variable/impredecible✅ Ideal❌ Costo fijoTráfico constante alto❌ Puede ser caro✅ Más económicoMicroservicios/eventos✅ Excelente⚠️ Más código
   Azure Data Lake Storage Gen2
   Cada operación CRUD genera automáticamente eventos estructurados:
   salesforce-events/
   ├── events/2026/01/15/account/
   │   ├── create_abc123.json
   │   ├── update_def456.json
   │   └── delete_ghi789.json
   ├── archives/account/2026/01/
   └── batches/2026/01/15/
   Casos de uso: Auditoría completa, analytics, compliance (GDPR/SOX), ML sobre datos históricos.

4. APIs RESTful Profesionales
   Endpoints siguiendo convenciones estándar:
   GET    /api/accounts           → Lista recursos
   GET    /api/accounts/{id}      → Recurso específico
   POST   /api/accounts           → Crear + enviar evento a Data Lake
   PATCH  /api/accounts/{id}      → Actualizar parcialmente
   DELETE /api/accounts/{id}      → Eliminar + registrar en Data Lake
   Códigos HTTP apropiados: 200, 201, 204, 400, 404, 500
   Versionamiento: /services/data/v58.0/... (estilo Salesforce)
   Documentación: Swagger/OpenAPI interactivo automático

📊 Resultados Demostrados
Arquitectura
✅ 4 capas separadas: Domain, Application, Infrastructure, Presentation
✅ 2 implementaciones completas: ASP.NET Core + Azure Functions
✅ 0 dependencias circulares
✅ 100% abstracciones para componentes críticos
Funcionalidad
✅ 15+ endpoints RESTful funcionales
✅ 3 entidades: Account, Contact, Opportunity
✅ CRUD completo con integración automática a Data Lake
✅ SOQL queries básicas implementadas
Código
✅ ~3,000 líneas C# documentadas
✅ Comentarios en inglés profesionales
✅ Error handling comprehensivo
✅ Testing scripts en Python, PowerShell, Bash

💡 Lecciones Clave Aprendidas
1. Clean Architecture paga dividendos

Esfuerzo inicial: Más archivos y estructura
Beneficio: Migración entre arquitecturas en horas, no días
Clave: Domain puro reutilizable al 100%

2. SOLID tiene impacto medible

Sin SOLID: Cambiar storage afecta 20+ archivos
Con SOLID: Cambiar storage afecta 3 archivos (repositories)

3. Serverless vs Tradicional - No hay "mejor" universal

Functions: Ideal para carga variable, eventos, microservicios
API tradicional: Mejor para tráfico alto constante, control total

4. Data Lake complementa, no reemplaza databases

Data Lake: Analytics, ML, auditoría, semi-estructurado
Database: Transacciones ACID, relaciones, queries complejos


🎓 Competencias Demostradas
Técnicas

Arquitectura empresarial (Clean Architecture)
Principios SOLID aplicados consistentemente
APIs RESTful production-ready
Cloud computing (Azure Functions + Data Lake)
Programación asíncrona (async/await)
Dependency Injection
Patrones: Repository, Service Layer, DTO

Profesionales

Autodidacta - aprendizaje proactivo
Documentación técnica clara
Pensamiento arquitectónico a largo plazo
Código mantenible y escalable
Atención a best practices industriales


🚀 Por Qué Este Proyecto Importa
No es solo código que funciona - es código pensado para durar
Decisiones tomadas considerando:

✅ Mantenibilidad: ¿Otro desarrollador lo entenderá en 6 meses?
✅ Escalabilidad: ¿Soporta crecimiento sin reescritura?
✅ Testabilidad: ¿Cada componente es testeable independientemente?
✅ Flexibilidad: ¿Puedo cambiar tecnologías sin rehacer todo?

Demuestra comprensión de arquitectura enterprise real

No es un tutorial seguido paso a paso
Es aplicación práctica de principios aprendidos
Incluye decisiones de diseño justificadas
Considera trade-offs realistas


📝 Nota para el Entrevistador
Este proyecto representa:

Aprendizaje Práctico - No solo teoría, sino implementación funcional
Pensamiento Arquitectónico - Código diseñado para el largo plazo
Profesionalismo - Documentación, convenciones, código production-ready

Disponible para:

Discutir decisiones arquitectónicas específicas
Explicar trade-offs considerados en el diseño
Demostrar el proyecto ejecutándose
Profundizar en cualquier aspecto técnico

¿Por qué dediqué tiempo a esto?
Porque entiendo que el código que escribo hoy será mantenido en el futuro - por otros o por mí mismo. Las decisiones arquitectónicas tienen consecuencias a largo plazo, y este proyecto demuestra que pienso más allá de "resolver el ticket de hoy".

Stack Técnico: .NET 8, C# 12, ASP.NET Core, Azure Functions, Azure Data Lake Gen2, Swagger/OpenAPI