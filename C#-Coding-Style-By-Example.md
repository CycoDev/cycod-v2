# C# Coding Style by Example

```csharp
//////////////////////////////
// 1. Variables and Types
//////////////////////////////

// GOOD: Use var for local variables
var customer = GetCustomerById(123);
var isValid = Validate(customer);
var orders = customer.Orders.Where(o => o.IsActive).ToList();

// BAD: Avoid explicit types when var is clearer
Customer customer = GetCustomerById(123);
List<Order> orders = customer.Orders.Where(o => o.IsActive).ToList();

// Private fields use underscore prefix
private readonly IUserService _userService;
private int _retryCount;
private string _connectionString;

// Constants use PascalCase
public const int MaxRetryAttempts = 3;
public const string DefaultRegion = "US-West";

// Use descriptive names that explain purpose
var isEligibleForDiscount = customer.Status == CustomerStatus.Premium && order.Total > 1000;
var hasShippingAddress = !string.IsNullOrEmpty(order.ShippingAddress);


//////////////////////////////
// 2. Method and Property Declarations
//////////////////////////////

// Methods start with verbs, use PascalCase
public User GetUserById(int id) { return _repository.Find(id); }
public void ProcessPayment(Payment payment) { _processor.Process(payment); }

// Boolean members use Is/Has/Can prefix
public bool IsActive { get; set; }
public bool HasPermission(string permission) { return _permissions.Contains(permission); }
public bool CanUserEditDocument(User user, Document doc) { return user.Id == doc.OwnerId; }

// Auto-properties for simple cases
public string Name { get; set; }
public DateTime CreatedAt { get; set; }

// Backing fields only when custom logic needed
private string _email;
public string Email 
{
    get => _email;
    set 
    {
        ValidateEmailFormat(value);
        _email = value;
    }
}

// Keep methods short (<20 lines) and focused
public decimal CalculateDiscount(Order order) 
{
    if (order == null) return 0;
    if (!order.Items.Any()) return 0;
    
    var subtotal = order.Items.Sum(i => i.Price);
    var discountRate = DetermineDiscountRate(order);
    
    return Math.Round(subtotal * discountRate, 2);
}


//////////////////////////////
// 3. Control Flow
//////////////////////////////

// Early returns reduce nesting
public ValidationResult Validate(User user)
{
    if (user == null) return ValidationResult.Invalid("User cannot be null");
    if (string.IsNullOrEmpty(user.Email)) return ValidationResult.Invalid("Email required");
    if (string.IsNullOrEmpty(user.Name)) return ValidationResult.Invalid("Name required");
    
    return ValidationResult.Success();
}

// Ternary for simple conditions
var displayName = user.Name ?? "Guest";
var statusLabel = user.IsActive ? "Active" : "Inactive";

// If/else for complex conditions
if (user.IsAuthenticated && 
    user.HasPermission("edit") && 
    !document.IsLocked) 
{
    document.AllowEditing();
}

// Single-line if for very simple cases
if (order == null) return null;
if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name is required");

// Meaningful variables for conditions
var isEligibleForDiscount = user.IsVip && order.Total > 1000;
var hasRequiredDocuments = passport != null && visa != null;
if (isEligibleForDiscount && hasRequiredDocuments) 
{
    ApplySpecialDiscount();
}


//////////////////////////////
// 4. Collections
//////////////////////////////

// Collection initializers
var colors = new List<string> { "Red", "Green", "Blue" };
var ages = new Dictionary<string, int> 
{
    ["John"] = 30,
    ["Alice"] = 25
};

// Empty collections
var emptyList = new List<string>();
var emptyDict = new Dictionary<string, int>();

// Copy collections
var original = new List<string> { "one", "two" };
var copy = new List<string>(original);


//////////////////////////////
// 5. Exception Handling and Error Returns
//////////////////////////////

// Return null for "not found" scenarios
public User FindUser(string username)
{
    return _repository.GetByUsername(username);  // May be null
}

// Throw for invalid inputs & exceptional conditions
public void ProcessPayment(decimal amount)
{
    if (amount <= 0) throw new ArgumentException("Amount must be positive", nameof(amount));
    
    if (paymentGateway.IsDown)
    {
        throw new PaymentException("Payment gateway unavailable");
    }
}

// Try pattern for operations expected to fail sometimes
public bool TryParseOrderId(string input, out int orderId)
{
    orderId = 0;
    if (string.IsNullOrEmpty(input)) return false;
    
    return int.TryParse(input, out orderId);
}

// Only catch exceptions you can handle
try 
{
    ProcessFile(fileName);
}
catch (FileNotFoundException ex)
{
    // Handle missing file specifically
    Logger.Warn($"File not found: {ex.FileName}");
}
catch (IOException ex)
{
    // Handle IO issues
    Logger.Error($"IO error: {ex.Message}");
}
// Let other exceptions bubble up


//////////////////////////////
// 6. Class Structure
//////////////////////////////

// Organize by access level, then by type
public class Customer
{
    // Public properties
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Public methods
    public void UpdateProfile(ProfileData data) { /* ... */ }
    public bool CanPlaceOrder() { /* ... */ }
    
    // Protected properties
    protected DateTime LastUpdated { get; set; }
    
    // Protected methods
    protected void OnProfileUpdated() { /* ... */ }
    
    // Private fields (at the bottom)
    private readonly ICustomerRepository _repository;
    private List<Order> _cachedOrders;
}


//////////////////////////////
// 7. Comments and Documentation
//////////////////////////////

// XML documentation for public APIs
/// <summary>
/// Processes a payment for an order.
/// </summary>
/// <param name="orderId">The order identifier to process payment for</param>
/// <param name="paymentMethod">The payment method to use</param>
/// <returns>Transaction receipt with confirmation details</returns>
/// <exception cref="PaymentDeclinedException">Thrown when payment is declined</exception>
public Receipt ProcessPayment(int orderId, PaymentMethod paymentMethod)
{
    var order = _orderRepository.GetById(orderId);
    var paymentProcessor = _paymentFactory.CreateProcessor(paymentMethod);
    
    // Self-documenting code with minimal comments
    var orderTotal = CalculateOrderTotal(order);
    var wasAuthorized = paymentProcessor.Authorize(orderTotal);
    
    if (!wasAuthorized)
    {
        throw new PaymentDeclinedException(paymentProcessor.DeclineReason);
    }
    
    var receipt = paymentProcessor.Capture(orderTotal);
    _orderRepository.MarkAsPaid(orderId, receipt.TransactionId);
    
    return receipt;
}

// Comments only for complex logic that isn't obvious
public decimal CalculateShipping(Order order)
{
    var baseShipping = order.Weight * _shippingRatePerKg;
    
    // Apply progressive discount for heavier packages
    // (Complex business rule that needs explanation)
    if (order.Weight > 10)
    {
        var discountTiers = Math.Floor((order.Weight - 10) / 5);
        var discountMultiplier = Math.Min(discountTiers * 0.05, 0.5);
        baseShipping *= (1 - discountMultiplier);
    }
    
    return baseShipping;
}


//////////////////////////////
// 8. LINQ
//////////////////////////////

// Single line for simple queries
var activeUsers = users.Where(u => u.IsActive).ToList();

// Multi-line for complex queries
var topCustomers = customers
    .Where(c => c.IsActive)
    .OrderByDescending(c => c.TotalSpent)
    .Take(10)
    .Select(c => new CustomerSummary(c))
    .ToList();

// Extract intermediate variables for complex queries
var activeAccounts = accounts.Where(a => a.IsActive);
var highValueAccounts = activeAccounts.Where(a => a.Balance > 100000);
var riskyAccounts = highValueAccounts.Where(a => a.HasRecentSuspiciousActivity);


//////////////////////////////
// 9. String Handling
//////////////////////////////

// Use string interpolation
var greeting = $"Hello, {user.Name}!";
var logMessage = $"User {userId} logged in at {loginTime:yyyy-MM-dd HH:mm}";

// Avoid string concatenation for multiple values
// BAD:
var message = "Hello, " + user.FirstName + " " + user.LastName + "!";

// GOOD:
var message = $"Hello, {user.FirstName} {user.LastName}!";


//////////////////////////////
// 10. Expression-Bodied Members
//////////////////////////////

// Use for simple property getters
public string FullName => $"{FirstName} {LastName}";
public bool IsAdult => Age >= 18;

// Use for simple methods
public string GetGreeting() => $"Hello, {Name}!";
public decimal GetTotal() => Items.Sum(i => i.Price);

// Avoid for complex logic
// BAD:
public string GetFormattedAddress() => 
    $"{Street}, {City}, {State} {ZipCode}".Trim(',', ' ');

// GOOD:
public string GetFormattedAddress()
{
    return $"{Street}, {City}, {State} {ZipCode}".Trim(',', ' ');
}


//////////////////////////////
// 11. Null Handling
//////////////////////////////

// Nullable annotations make intent clear
public User? FindUser(string username)
{
    return _repository.GetByUsername(username);
}

// Null-conditional for safe navigation
var city = address?.City ?? "Unknown";
var zipCode = order?.ShippingAddress?.ZipCode;

// Null-coalescing for defaults
var displayName = user.Name ?? "Guest";
var sortOrder = request.SortOrder ?? DefaultSortOrder;

// Null-coalescing assignment for lazy initialization
private List<Order> _cachedOrders;
public List<Order> CachedOrders 
{
    get 
    {
        _cachedOrders ??= LoadOrdersFromDatabase();
        return _cachedOrders;
    }
}

// Explicit checks for important validation
public void ProcessOrder(Order order)
{
    if (order == null) throw new ArgumentNullException(nameof(order));
    if (order.Customer == null) throw new ArgumentException("Order must have a customer");
    
    // Process order...
}


//////////////////////////////
// 12. Asynchronous Programming
//////////////////////////////

// Use async/await throughout
public async Task<User> GetUserAsync(int id)
{
    var user = await _repository.GetByIdAsync(id);
    var settings = await _settingsService.GetUserSettingsAsync(id);
    
    user.Settings = settings;
    return user;
}

// Never use ConfigureAwait(false)
// BAD:
var data = await GetDataAsync().ConfigureAwait(false);

// GOOD:
var data = await GetDataAsync();


//////////////////////////////
// 13. Static Methods and Classes
//////////////////////////////

// Use static class for utility classes with only static methods
public static class StringHelpers
{
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
    
    public static string Slugify(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        var slug = value.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("&", "and");
            
        return slug;
    }
}

// BAD: Non-static helper class
public class FileHelpers
{
    public static string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}

// GOOD: Static helper class
public static class FileHelpers
{
    public static string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}


//////////////////////////////
// 14. Parameters
//////////////////////////////

// Use nullable reference types for optional parameters
public User CreateUser(string name, string email, string? phoneNumber = null)
{
    var user = new User
    {
        Name = name,
        Email = email
    };
    
    if (phoneNumber != null)
    {
        user.PhoneNumber = phoneNumber;
    }
    
    return user;
}

// Use optional parameters with defaults
public PaginatedResult<Product> GetProducts(
    int page = 1,
    int pageSize = 20,
    string sortBy = "name",
    bool ascending = true)
{
    // ...
}


//////////////////////////////
// 15. Code Organization
//////////////////////////////

// Static classes for utilities/helpers at edges
public static class DateHelpers { /* ... */ }
public static class ValidationHelpers { /* ... */ }

// Instance classes for core business logic
public class OrderProcessor { /* ... */ }
public class CustomerService { /* ... */ }


//////////////////////////////
// 16. Method Returns
//////////////////////////////

// Use early returns to reduce nesting
public ValidationResult ValidateRegistration(RegistrationRequest request)
{
    if (request == null) return ValidationResult.Invalid("Request is required");
    if (string.IsNullOrEmpty(request.Email)) return ValidationResult.Invalid("Email is required");
    if (string.IsNullOrEmpty(request.Password)) return ValidationResult.Invalid("Password is required");
    if (request.Password.Length < 8) return ValidationResult.Invalid("Password too short");
    if (_userRepository.EmailExists(request.Email)) return ValidationResult.Invalid("Email already registered");
    
    return ValidationResult.Success();
}

// Use ternary for simple returns
public string GetDisplayName(User user)
{
    return !string.IsNullOrEmpty(user.FirstName) ? user.FirstName : user.Email;
}


//////////////////////////////
// 17. Parameter Handling
//////////////////////////////

// Use nullable annotations for optional parameters
public void SendNotification(User user, string message, NotificationPriority? priority = null)
{
    var actualPriority = priority ?? NotificationPriority.Normal;
    // ...
}

// Use descriptive names for boolean parameters
// BAD:
SubmitOrder(order, true);

// GOOD:
SubmitOrder(order, sendConfirmationEmail: true);


//////////////////////////////
// 18. Method Chaining
//////////////////////////////

// Format multi-line method chains with the dot at the beginning of each new line
var result = collection
    .Where(x => x.IsActive)
    .Select(x => x.Name)
    .OrderBy(x => x)
    .ToList();

// For builder patterns
var process = new ProcessBuilder()
    .WithFileName("cmd.exe")
    .WithArguments("/c echo Hello")
    .WithTimeout(1000)
    .Build();


//////////////////////////////
// 19. Resource Cleanup
//////////////////////////////

// Use using declarations (C# 8.0+)
public string ReadFileContent(string path)
{
    using var reader = new StreamReader(path);
    return reader.ReadToEnd();
}

// Use try/finally for complex cleanup
public void ProcessLargeFile(string path)
{
    var stream = new FileStream(path, FileMode.Open);
    try 
    {
        // Do complex processing with multiple steps
    }
    finally
    {
        stream.Dispose();
    }
}


//////////////////////////////
// 20. Field Initialization
//////////////////////////////

// Initialize simple fields at declaration
private int _retryCount = 3;
private readonly List<string> _errorMessages = new List<string>();

// Complex initialization in constructors
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IValidator<User> _validator;
    private readonly UserSettings _settings;
    
    public UserService(
        IUserRepository repository,
        IValidator<User> validator,
        IOptions<UserSettings> options)
    {
        _repository = repository;
        _validator = validator;
        _settings = options.Value;
    }
}


//////////////////////////////
// 21. Logging Conventions
//////////////////////////////

// Include context values, not class/method names
// BAD:
Logger.Info("UserService.CreateUser: User created");

// GOOD:
Logger.Info($"User created: {user.Id} ({user.Email})");

// Include relevant values for debugging
Logger.Debug($"Processing order {order.Id} with {order.Items.Count} items, total: {order.Total:C}");


//////////////////////////////
// 22. Class Design and Relationships
//////////////////////////////

// Inheritance for "is-a" relationships
public abstract class Document
{
    public string Id { get; set; }
    public string Title { get; set; }
    public abstract string GetDocumentType();
}

public class Invoice : Document
{
    public decimal Amount { get; set; }
    public override string GetDocumentType() => "Invoice";
}

// Composition for "has-a" relationships
public class Order
{
    private readonly IPaymentProcessor _paymentProcessor;
    private readonly IShippingCalculator _shippingCalculator;
    
    public Order(IPaymentProcessor paymentProcessor, IShippingCalculator shippingCalculator)
    {
        _paymentProcessor = paymentProcessor;
        _shippingCalculator = shippingCalculator;
    }
    
    public void Process()
    {
        _paymentProcessor.ProcessPayment(this);
    }
    
    public decimal CalculateShipping()
    {
        return _shippingCalculator.Calculate(this);
    }
}


//////////////////////////////
// 23. Condition Checking Style
//////////////////////////////

// Store condition results in descriptive variables
public bool CanUserEditDocument(User user, Document document)
{
    var isDocumentOwner = document.OwnerId == user.Id;
    var hasEditPermission = user.Permissions.Contains("edit");
    var isAdminUser = user.Role == UserRole.Admin;
    var isDocumentLocked = document.Status == DocumentStatus.Locked;
    
    return (isDocumentOwner || hasEditPermission || isAdminUser) && !isDocumentLocked;
}

// Early returns for guard clauses
public void SendNotification(User user, Notification notification)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (notification == null) throw new ArgumentNullException(nameof(notification));
    
    var notifier = _notifierFactory.CreateNotifier(user.PreferredChannel);
    notifier.Send(notification);
    _notificationLog.RecordSent(user.Id, notification.Id);
}


//////////////////////////////
// 24. Builder Patterns and Fluent Interfaces
//////////////////////////////

// Return this from builder methods
public class EmailBuilder
{
    private readonly Email _email = new Email();
    
    public EmailBuilder WithSubject(string subject)
    {
        _email.Subject = subject;
        return this;
    }
    
    public EmailBuilder WithBody(string body)
    {
        _email.Body = body;
        return this;
    }
    
    public EmailBuilder WithRecipient(string recipient)
    {
        _email.Recipients.Add(recipient);
        return this;
    }
    
    public Email Build()
    {
        return _email;
    }
}

// Usage
var email = new EmailBuilder()
    .WithSubject("Hello")
    .WithBody("This is a test")
    .WithRecipient("user@example.com")
    .Build();


//////////////////////////////
// 25. Using Directives and Namespaces
//////////////////////////////

// Group System namespaces first, then others, alphabetized within groups
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// No namespaces in the codebase - use top-level statements


//////////////////////////////
// 26. Default Values and Constants
//////////////////////////////

// Use explicit defaults
var name = userName ?? "Anonymous";
var count = requestedCount > 0 ? requestedCount : 10;

// Use named constants for magic numbers
private const int MaxRetryAttempts = 3;
private const double StandardDiscountRate = 0.1;
private const string ApiEndpoint = "https://api.example.com/v2";

// Boolean parameters default to false (safer)
public void ProcessOrder(Order order, bool sendConfirmation = false)
{
    // ...
}


//////////////////////////////
// 27. Extension Methods
//////////////////////////////

// Use only when providing significant readability benefits
public static class StringExtensions
{
    public static bool IsValidEmail(this string email)
    {
        return !string.IsNullOrEmpty(email) && email.Contains("@") && email.Contains(".");
    }
    
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}

// Usage
if (user.Email.IsValidEmail())
{
    // Process valid email
}


//////////////////////////////
// 28. Attributes
//////////////////////////////

// Class attributes on separate lines
[Serializable]
[ApiController]
public class ProductController
{
    // Property attributes on same line as property
    [Required] public string Name { get; set; }
    
    // Method with parameter attributes
    public IActionResult Get([FromQuery] int id)
    {
        // ...
    }
}


//////////////////////////////
// 29. Generics
//////////////////////////////

// Use constraints when needed
public class Repository<T> where T : class, IEntity, new()
{
    public T GetById(int id)
    {
        // ...
    }
    
    public void Save(T entity)
    {
        // ...
    }
}

// Use descriptive names for complex cases
public interface IConverter<TSource, TDestination>
{
    TDestination Convert(TSource source);
}

// Single letter parameters for simple cases
public class Cache<T>
{
    private readonly Dictionary<string, T> _items = new Dictionary<string, T>();
    
    public void Add(string key, T value)
    {
        _items[key] = value;
    }
    
    public T Get(string key)
    {
        return _items.TryGetValue(key, out var value) ? value : default;
    }
}


//////////////////////////////
// 30. Project Organization
//////////////////////////////

// Group files by feature/functionality
// Example project structure:
//
// /Customers
//   CustomerController.cs
//   CustomerService.cs
//   CustomerRepository.cs
//   CustomerValidator.cs
//   Models/
//     Customer.cs
//     CustomerViewModel.cs
//
// /Orders
//   OrderController.cs
//   OrderService.cs
//   OrderRepository.cs
//   Models/
//     Order.cs
//     OrderViewModel.cs
```