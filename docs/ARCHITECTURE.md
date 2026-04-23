# Architecture Overview: Rok Music Player (WinUI3)

## 📖 General Description
Rok is a music player application developed using .NET C# and the WinUI3 framework. The application follows a layered architecture designed to clearly separate concerns, which is crucial for maintainability and testability.

## 🧱 Layered Structure (Project Organization)

The solution is composed of multiple projects, each playing a distinct role:

### 1. `Presentation` (WinUI3)
*   **Role:** The User Interface and Presentation Layer (UI/UX). This is the visible entry point of the application.
*   **Content:** Contains XAML code (`*.xaml`), code-behinds, and ViewModels.
*   **Implementation:** Adheres to the MVVM (Model-View-ViewModel) pattern, ensuring that the business logic is separated from the UI details.
*   **Functionality:** Manages user interaction and displays data retrieved from lower layers.

### 2. `Domain`
*   **Role:** Defines the core business rules and the fundamental entities of the application.
*   **Content:** Core business objects (`Entities`) and high-level data models.
*   **Principle:** This layer is the purest and must not depend on any other layer, guaranteeing data consistency across the entire system.

### 3. `Application`
*   **Role:** Orchestrates the business logic and manages the use cases.
*   **Content:** Services that contain complex business logic (e.g., `PlayerService`, `ImportService`). It acts as a coordinator between the View and the Domain.
*   **Dependencies:** Depends on `Domain` for models and often interacts with `Infrastructure` to save/load data.

### 4. `Infrastructure`
*   **Role:** Implements the technical details for the business logic to operate. This is the Persistence Layer.
*   **Content:** Database access implementation (DbContext, Repositories), handling of external services (e.g., location services, networking).
*   **Dependencies:** Depends on `Domain` to work with business entities.

### 5. `Import`
*   **Role:** Contains specialized logic dedicated to importing data from external sources (files, APIs, etc.).
*   **Content:** Services specialized for parsing and validating incoming data (`AlbumImport`, `ArtistImport`, etc.) before they are persisted into the Domain model.

## 🔄 Data Flow
1. The user interacts with the **`Presentation`** (ViewModel).
2. The ViewModel calls a service within the **`Application`** layer.
3. The **`Application`** service executes the business logic, utilizing **`Domain`** models.
4. If data persistence is required, the service interacts with **`Repositories`** in **`Infrastructure`**.
5. The `Import` services intervene to standardize external data before it enters the Domain lifecycle.

## 🎯 Key Takeaways
*   **Design Pattern:** Clean/Layered Architecture.
*   **Strengths:** Highly modular, testable, and maintainable due to strict separation of concerns.
*   **Next Steps:** We should focus on defining specific use cases (e.g., 'Fetching Playlist Data') to start implementing the logic within the `Application` layer, starting with setting up Todo tasks.