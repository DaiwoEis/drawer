---
name: unity-gamedev
description: "Create production-ready Unity C# scripts, systems, and architecture. Use this when users ask for Unity code, game mechanics, component architecture, optimization advice, or design patterns. Focuses on modularity, ScriptableObject architecture, and high performance."
---

# Unity Game Development Skill

This skill guides the creation of high-quality, performant, and maintainable Unity C# code.

## Core Philosophy

- **Component-Based**: Scripts should be modular components (MonoBehaviours) that do one thing well.
- **Inspector-Driven**: Expose parameters to the Inspector using `[SerializeField] private` to separate data from logic.
- **Event-Driven**: Decouple systems using `UnityEvent`, C# Events, or ScriptableObject-based Event Channels.
- **Data-Oriented**: Use `ScriptableObject` for shared data, configuration, and game state variables.
- **Performance-First**: Avoid garbage collection allocations in hot paths (Update loops).

## Coding Guidelines

### 1. Class Structure & Fields
- Use namespaces to organize code (e.g., `Game.Systems`, `Game.UI`).
- Prefer `[SerializeField] private` over `public` variables for Inspector access.
- Use `[Header]`, `[Tooltip]`, and `[Range]` attributes to improve the Inspector UI.
- Cache references in `Awake` using `GetComponent` or direct assignment, never in `Update`.

### 2. Lifecycle Management
- **Awake**: Initialization of self (getting components, setting up internal state).
- **Start**: Initialization dependent on other objects (they are guaranteed to exist).
- **Update**: Input and frame-based logic. Keep it lightweight.
- **FixedUpdate**: Physics calculations only (using `Time.fixedDeltaTime`).
- **LateUpdate**: Camera follow or logic that must happen after everything else moves.
- **OnEnable/OnDisable**: Register/Unregister events here to prevent memory leaks.

### 3. Performance & Optimization
- **Avoid `Find`**: Never use `GameObject.Find` or `FindObjectOfType` in `Update`. Cache references.
- **Tag Comparison**: Use `CompareTag("Tag")` instead of `tag == "Tag"` (avoids GC allocation).
- **String Concatenation**: Minimize string operations in `Update`. Use `StringBuilder` if necessary.
- **Object Pooling**: Do not `Instantiate` and `Destroy` frequently. Use an Object Pool (see `examples/ObjectPool.cs`).
- **Coroutines**: Use Coroutines for temporal logic instead of timers in `Update`. Prefer `YieldInstruction` caching.
- **Structs vs Classes**: Use `struct` for simple data containers to avoid GC overhead.

### 4. Advanced Architecture Patterns

#### ScriptableObject Architecture
Use `ScriptableObject` not just for data containers, but for architecture:
- **Event Channels**: Create SOs that act as events (e.g., `PlayerDiedEventSO`). Systems subscribe to the SO, not the Player instance.
- **Runtime Sets**: Maintain a list of active objects (e.g., `AllEnemiesSetSO`) so managers can iterate over them without `FindObjectsOfType`.

#### Finite State Machine (FSM)
For complex behaviors (Player Controller, AI), use a State Machine to organize logic:
- Separate logic into `IState` classes (Idle, Run, Jump).
- Use a `StateMachine` to manage transitions.
- See `examples/StateMachine.cs` for implementation.

#### Dependency Injection
- Avoid Singleton abuse (`GameManager.Instance`).
- Prefer initializing components via `Init()` methods or using a lightweight Service Locator / DI framework (like VContainer or Zenject if available, or simple constructor injection for pure C# classes).

### 5. Anti-Patterns to Avoid

#### The God Class (Monolithic MonoBehaviour)
- **Symptom**: A single script (e.g., `GameManager.cs` or `Player.cs`) exceeding 500+ lines, handling input, logic, UI, and data storage simultaneously.
- **Solution**: Break it down using the **Single Responsibility Principle (SRP)**.
  - **Input**: `PlayerInputReader` (reads keys, invokes events).
  - **Logic**: `PlayerMovement`, `PlayerHealth`, `PlayerInventory` (handle specific mechanics).
  - **View/UI**: `PlayerAnimator`, `PlayerUIManager` (listen to logic events, update visuals).
  - **Data**: `PlayerStatsSO` (ScriptableObject holding config like speed, max health).
- **Refactoring Strategy**: Identify distinct roles in the God Class and extract them into separate components or pure C# classes. Use events to communicate between them.

### 6. General Software Design Principles

#### SOLID Principles
- **SRP (Single Responsibility Principle)**: A class should have one, and only one, reason to change. (As seen in the God Class anti-pattern).
- **OCP (Open/Closed Principle)**: Classes should be open for extension but closed for modification. Use interfaces or abstract base classes (e.g., `IEnemy`) so you can add new enemy types without changing the `EnemyManager` code.
- **LSP (Liskov Substitution Principle)**: Subtypes must be substitutable for their base types. If `FlyingEnemy` inherits from `Enemy`, it shouldn't throw an exception when `Walk()` is called (interface segregation helps here).
- **ISP (Interface Segregation Principle)**: Clients should not be forced to depend on interfaces they do not use. Prefer small, specific interfaces (e.g., `IDamageable`, `IMovable`) over one giant `IEntity` interface.
- **DIP (Dependency Inversion Principle)**: Depend on abstractions, not concretions. `Player` should depend on `IInputProvider`, not `KeyboardInput`. This allows easy swapping for `GamepadInput` or `AIInput`.

#### Other Key Principles
- **Composition over Inheritance**: In Unity, prefer adding small functional components (`Health`, `Mover`, `Attacker`) to a GameObject rather than creating deep inheritance trees (`Entity -> LivingEntity -> Creature -> Enemy -> Goblin`).
- **DRY (Don't Repeat Yourself)**: Extract common logic into utility methods or shared components.
- **KISS (Keep It Simple, Stupid)**: Avoid over-engineering. Solved the problem in the simplest way possible first.

## Example Template: Modular Player Controller

```csharp
using UnityEngine;
using UnityEngine.Events;

namespace Game.Mechanics
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField, Tooltip("Speed in units per second")] 
        private float moveSpeed = 5f;
        
        [SerializeField] 
        private float jumpForce = 10f;

        [Header("Events")]
        public UnityEvent onJump;
        public UnityEvent<bool> onGroundedChanged;

        private Rigidbody _rb;
        private bool _isGrounded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            {
                Jump();
            }
        }

        private void Jump()
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            onJump?.Invoke();
        }
        
        private void OnCollisionEnter(Collision collision)
        {
             CheckGround(collision, true);
        }
        
        private void OnCollisionExit(Collision collision)
        {
             CheckGround(collision, false);
        }

        private void CheckGround(Collision collision, bool state)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                if (_isGrounded != state)
                {
                    _isGrounded = state;
                    onGroundedChanged?.Invoke(_isGrounded);
                }
            }
        }
    }
}
```

## Guidelines for Responses

When providing Unity code:
1.  **Always** include necessary `using` directives.
2.  **Explain** *why* you are using `Awake` vs `Start` or `FixedUpdate`.
3.  **Suggest** where to attach the script in the Unity Editor.
4.  **Reference** architectural patterns (e.g., "This uses the State Machine pattern to manage AI states...").
5.  If the logic is complex, suggest using **ScriptableObjects** for data/events.