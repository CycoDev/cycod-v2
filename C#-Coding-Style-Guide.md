# C# Coding Style Guide

## 1. Variables and Types

- **Variable Declaration**: Use `var` in almost all cases for local variable declarations
- **Private Fields**: Use underscore prefix and camelCase (`_fieldName`)
- **Naming**: Use descriptive variable names that indicate purpose
- **Constants**: Use PascalCase for constants

## 2. Method and Property Declarations

- **Public Members**: Use PascalCase for all public members
- **Method Names**: Prefix methods with verbs (Get, Set, Update)
- **Boolean Properties**: Use "Is" prefix (IsValid, IsRunning)
- **Properties**: Use auto-properties whenever possible, only use backing fields when custom logic is needed
- **Method Length**: 
  - Keep methods short (<20 lines) and focused on single tasks
  - Break down long methods into smaller helper methods
  - Extract major conditional branches into separate methods
  - Keep the main method focused on the decision flow while helper methods handle specific operations

## 3. Control Flow

- **Ternary Operators**: Use for simple conditions, use if/else for complex ones
  - Well-named variables in ternary conditions improve readability
  - Prefer ternaries for right-hand-side assignments
- **Single-line Statements**: For very simple conditions, prefer having the if statement and action on the same line
  - Example: `if (isInvalid) return null;`
- **Boolean Expressions**: 
  - Use positive conditions when possible (`if (isValid)` rather than `if (!isInvalid)`)
  - Use descriptive intermediate variables for complex conditions

## 4. Collections

- Use collection initializers when appropriate (`new List<int> { 1, 2, 3 }`)
- Use constructor-based initialization for empty collections
- Use copy constructors when duplicating collections

## 5. Exception Handling and Error Returns

- **Use consistent error handling patterns based on context:**
  - **Return null/default** for "not found" scenarios (e.g., finder methods)
  - **Throw exceptions** for invalid inputs, programmer errors, and exceptional conditions
  - **Use the `Try` pattern** (`bool TryParse(string input, out int result)`) for operations expected to fail sometimes
  - **Return boolean values** to indicate simple success/failure when no additional data is needed

- **For exceptions:**
  - Only catch exceptions you can actually handle meaningfully
  - Let other exceptions bubble up
  - Use specific exception types that describe the error condition
  - Include meaningful error messages that help diagnose issues

- **For helper methods:**
  - Be consistent with similar methods in the codebase
  - Consider the caller's perspective and expected usage patterns
  - Prefer the simplest approach that meets the needs
  - Document error handling behavior with XML comments

## 6. Class Structure

- Organization: First by access level (public, protected, private), then by type
- Fields always at the bottom of each access level section
- One class per file in most cases (occasionally allow related small classes in same file)
- Small, focused methods rather than large ones

## 7. Comments and Documentation

- Thorough XML documentation for all public members and types:
  - `<summary>` tag explaining purpose and behavior
  - `<param>` tags for all parameters with descriptions
  - `<returns>` tag for return values
  - `<exception>` tags for any exceptions thrown
  - `<example>` tags for complex APIs
- Inline comments only for complex logic
- Minimal comments elsewhere, relying on well-named identifiers
- Self-documenting code through descriptive naming

## 8. LINQ

- Single line for simple queries, multiple lines with indentation for complex ones
- Prefer method syntax (Where, Select) over query syntax (from, where, select)
- Extract intermediate variables with meaningful names for complex queries, especially if:
  - The intermediate result is used multiple times
  - It improves readability by breaking down complex logic
  - The intermediate result represents a meaningful concept

## 9. String Handling

- Use string interpolation (`$"Hello {name}"`) as the primary string formatting approach

## 10. Expression-Bodied Members

- Use only for property getters and very simple methods
- Prefer traditional block bodies for more complex logic

## 11. Null Handling

- Use a mix of approaches based on context and complexity
- Null-conditional operators (`?.`) for method/property chains
- Null-coalescing operator (`??`) for simple default value assignments
- Null-coalescing assignment operator (`??=`) where appropriate
- Explicit if/else null checks when logic is complex or when clarity is more important than conciseness
- Use nullable reference types (`string?`) to make nullability explicit in signatures

## 12. Asynchronous Programming

- Use async/await throughout with Task-based APIs
- Never use ConfigureAwait(false)

## 13. Static Methods and Classes

- Use static classes for utility/helper classes that contain only static methods
- Use static methods liberally for utility functions with no state
- Organize utility methods in classes with "Helpers" in the name
- Make helper classes explicitly `static class` if they only contain static methods

## 14. Parameters

- Use nullable reference types (`string?`) for optional parameters
- Use optional parameters with default values where appropriate

## 15. Code Organization and Structure

- Static classes for utilities/helpers at application edges
- Instance classes for core business logic in the middle
- Keep files focused on a single responsibility

## 16. Method Returns

- Use early returns to reduce nesting and complexity
- Use ternary expressions for returns in short methods where appropriate

## 17. Parameter Handling

- Use nullable annotations (`string?`) for parameters that can be null
- Use descriptive names for boolean parameters
- Use named arguments when calling methods with boolean parameters

## 18. Method Chaining

- Format multi-line method chains with the dot at the beginning of each new line:
  ```csharp
  var result = collection
      .Where(x => x.IsValid)
      .Select(x => x.Name)
      .ToList();
  ```

## 19. Resource Cleanup

- Prefer using declarations (C# 8.0+) where possible
- Use try/finally for complex cleanup scenarios
- Be pragmatic - skip explicit cleanup for non-critical resources

## 20. Field Initialization

- Initialize simple fields at declaration (e.g., `private int _count = 0;`)
- Move complex initializations to constructors
- Make default values visible at declaration point when possible
- Use field initializers for constants and simple static fields

## 21. Logging Conventions

- Include class and method name prefixes only for complex or unusual cases
- Rely on the logging system to capture caller information when possible
- Use descriptive messages that provide context
- For debug messages, include relevant values to aid troubleshooting
- Keep log messages concise but informative

## 22. Class Design and Relationships

- Use a balanced, pragmatic approach to inheritance vs. composition:
  - Use inheritance for true "is-a" relationships (where the derived class is a specialized type of the base class)
  - Use composition for "has-a" or "uses-a" relationships (where a class needs a capability)
  - Prefer interfaces over abstract classes when only defining a contract
  - Use abstract classes when providing shared implementation
- Keep inheritance hierarchies shallow (preferably no more than 2-3 levels deep)
- Favor readability and simplicity over complex design patterns
- Choose the appropriate abstraction for the situation rather than dogmatically following one approach

## 23. Condition Checking Style

- Prefer early returns for guard clauses and validation:
  ```csharp
  if (input == null) return null;
  if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name is required");
  ```

- Store condition results in descriptive variables when the condition adds semantic value:
  ```csharp
  var isValidUser = user != null && user.IsActive;
  if (isValidUser) { ... }
  ```

- Avoid complex inline conditions; extract to variables with meaningful names:
  ```csharp
  // Instead of:
  if (order.Status == OrderStatus.Completed && DateTime.Now > order.CompletionDate.AddDays(30)) { ... }
  
  // Prefer:
  var isReturnPeriodExpired = DateTime.Now > order.CompletionDate.AddDays(30);
  var isOrderComplete = order.Status == OrderStatus.Completed;
  if (isOrderComplete && isReturnPeriodExpired) { ... }
  ```

## 24. Builder Patterns and Fluent Interfaces

- Prefer fluent interfaces with method chaining for builder patterns
- Return `this` from builder methods to enable chaining
- Name builder methods with "With" prefix (e.g., `WithName()`, `WithTimeout()`)
- Format each method call on a separate line for readability
- Use a final Build() or similar method to create the immutable result
- Example:
  ```csharp
  var process = new ProcessBuilder()
      .WithFileName("cmd.exe")
      .WithArguments("/c echo Hello")
      .WithTimeout(1000)
      .Build();
  ```

## 25. Using Directives and Namespaces

- **Using Directives:**
  - Group using directives by type (System namespaces first, then others)
  - Alphabetize within each group
  - Keep a blank line between groups
  - Example:
    ```csharp
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    
    using Microsoft.Extensions.Logging;
    ```

- **Namespaces:**
  - Avoid creating explicit namespaces within the codebase
  - Use implicit top-level namespaces when working with .NET 6+ projects
  - Place classes at the top level without namespace declarations
  - Rely on project structure for organization rather than namespaces

## 26. Default Values and Constants

- Use explicit defaults (`null`, `0`, `false`) instead of default literals or expressions
- Use named constants for magic numbers and repeated values
- Be explicit about default parameter values
- When defining default values, pick sensible defaults that work in most cases
- For optional boolean parameters, choose the safer default (usually `false`)

## 27. Extension Methods

- Use extension methods only when they provide significant readability benefits
- Prefer using extension methods for fluent APIs and LINQ-style operations
- Keep extension methods in a dedicated static class with the naming pattern `[Type]Extensions`
- Document extension methods thoroughly with XML comments
- Consider using static helper methods instead when the operation doesn't conceptually belong to the type
- Make extension methods discoverable by using logical naming that reflects the extended type

## 28. Attributes

- For class, method, and property declarations:
  - Place attributes on a separate line before the declaration
  - Use one attribute per line for multiple attributes
  - Example:
    ```csharp
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class User
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
    ```

- For parameters:
  - Place attributes on the same line as the parameter
  - Example:
    ```csharp
    public void ProcessUser([NotNull] string username, [Optional] string department)
    ```

- For return values:
  - Place the attribute before the return type
  - Example:
    ```csharp
    [return: NotNull]
    public string GetUserName()
    ```

## 29. Generics

- Use generics only when they provide clear benefits in terms of type safety or code reuse
- Apply meaningful constraints to generic type parameters where appropriate
- Name generic type parameters with descriptive names for complex scenarios
- Use single letter type parameters (T, K, V) only for simple cases
- Consider providing non-generic alternatives for common use cases
- Document generic parameters thoroughly with XML comments
- Avoid excessive generic constraints that make the code difficult to understand

## 30. Project and Solution Organization

- Group files by feature/functionality rather than by type
- Organize related files into subdirectories that represent domains or features
- Use descriptive directory names that reflect the contained functionality
- Keep project dependencies clean and well-defined
- Follow a consistent file naming convention within each feature area
- Place interfaces and their implementations in the same directory
- Use solution folders to organize multiple projects logically
- Keep test projects structured to mirror the organization of the code being tested