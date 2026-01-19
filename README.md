🎯 Objetivo Principal
Construir una API RESTful completa aplicando Clean Architecture, principios SOLID y servicios de Azure, implementando el mismo sistema en dos arquitecturas diferentes para comprender sus trade-offs:

ASP.NET Core Web API (tradicional, servidor always-on)

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

🎓 Competencias Demostradas
Técnicas

Arquitectura empresarial (Clean Architecture)
Principios SOLID aplicados consistentemente
APIs RESTful production-ready
Cloud computing (Azure Functions + Data Lake)
Programación asíncrona (async/await)
Dependency Injection
Patrones: Repository, Service Layer, DTO

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

¿Por qué dediqué tiempo a esto?
Porque entiendo que el código que escribo hoy será mantenido en el futuro - por otros o por mí mismo. Las decisiones arquitectónicas tienen consecuencias a largo plazo, y este proyecto demuestra que pienso más allá de "resolver el ticket de hoy".

Stack Técnico: .NET 8, C# 12, ASP.NET Core, Azure Functions, Azure Data Lake Gen2, Swagger/OpenAPI