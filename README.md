# 💬 Chat.Api – Real-Time Messaging API with ASP.NET Core

**Chat.Api** is a scalable, production-ready real-time messaging backend built using **ASP.NET Core**, **SignalR**, and **Entity Framework Core**, designed to deliver reliable and responsive communication – just like WhatsApp.

---

## 🚀 Key Features

- 🔐 **Secure Authentication**  
  JWT-based authentication with ASP.NET Identity.

- 📡 **Real-Time Messaging**  
  Instant delivery using SignalR and connection-based presence tracking.

- 🗂 **Clean Architecture**  
  Structured with clear separation of concerns (Controller → Service → Repository).

- 🧠 **Smart Caching**  
  Uses `IMemoryCache` with automatic invalidation on message or conversation changes.

- 🔒 **Rate Limiting**  
  Prevents abuse/spam using `AspNetCoreRateLimit` middleware.

- ✅ **Data Validation**  
  Robust request validation using `FluentValidation` and/or `DataAnnotations`.

- 🧪 **Unit Tested Services**  
  Critical business logic is covered with unit tests.

---

## ⚙️ Tech Stack

| Layer          | Technology                        |
|----------------|-----------------------------------|
| Backend        | ASP.NET Core 8                    |
| Real-Time      | SignalR                           |
| ORM            | Entity Framework Core             |
| Authentication | JWT + ASP.NET Identity            |
| Validation     | FluentValidation                  |
| Caching        | In-Memory Cache                   |
| Rate Limiting  | AspNetCoreRateLimit               |
| Testing        | xUnit, Moq                        |

---

## 🧩 Project Structure

