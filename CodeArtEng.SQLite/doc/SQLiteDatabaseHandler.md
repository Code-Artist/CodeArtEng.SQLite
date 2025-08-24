# SQLiteDatabaseHandler API Documentation

## Overview

The `SQLiteDatabaseHandler` is an abstract class that extends `SQLiteHelper` to provide synchronized access to SQLite databases with support for both remote and local database connections. This class is designed to handle scenarios where you need to work with a database that may be stored on a network location while maintaining a local synchronized copy for improved performance and offline access.

Local synchronization dramatically improves performance by eliminating network latency for database operations. Instead of each query traversing the network to reach a remote database (which can add 10-100ms+ per operation), queries execute against a local file with sub-millisecond response times. This is particularly beneficial for applications that perform frequent read operations, complex queries, or need to maintain responsive user interfaces. The automatic sync mechanism ensures data freshness while maximizing performance.

This class is especially valuable in enterprise environments where databases are centrally hosted but accessed by distributed client applications. Common scenarios include desktop applications accessing shared databases on file servers, reporting tools that need fast access to centralized data, or applications that must remain functional during network outages. The handler abstracts away the complexity of sync management, network availability detection, and seamless failover between remote and local data sources.

## Key Features

- **Remote Database Access**: Connect to databases stored on network locations
- **Local Database Synchronization**: Automatically maintain a local copy of the remote database
- **Automatic Sync Management**: Configurable sync intervals to keep local copy updated
- **Online/Offline Handling**: Graceful handling of remote database availability
- **Read-Only Local Operations**: Local database is maintained as read-only for data integrity


## Usage Examples

### Example 1: Remote-Only Database Handler

This example demonstrates connecting directly to a remote database without local synchronization. This approach is suitable for applications that always require the most current data and have reliable network connectivity.

**Practical Use Cases:**
- Administrative tools that must work with live data
- Applications with infrequent database access
- Scenarios where local storage is restricted or not desired

```csharp
public class MyDatabaseHandler : SQLiteDatabaseHandler
{
    public MyDatabaseHandler(string remotePath) : base(remotePath)
    {
    }
    
    public void GetUserData()
    {
        // Connect to remote database only
        string query = "SELECT * FROM Users";
        var results = ExecuteQuery(query);
        // Process results...
    }
}

// Usage
var handler = new MyDatabaseHandler(@"\\server\share\database.db");
if (handler.IsRemoteDatabaseOnline())
{
    handler.GetUserData();
}
```

### Example 2: Remote Database with Local Sync

This example shows the recommended approach for most applications, providing both performance benefits and offline capabilities through local synchronization.

**Practical Use Cases:**
- Desktop applications in corporate environments accessing shared databases
- Reporting applications that run frequent queries against large datasets
- Mobile applications that need offline functionality
- Point-of-sale systems that must remain operational during network outages

```csharp
public class SyncedDatabaseHandler : SQLiteDatabaseHandler
{
    public SyncedDatabaseHandler(string remotePath, string localPath) 
        : base(remotePath, localPath, updateIntervalMin: 15) // Sync every 15 minutes
    {
    }
    
    public List<Customer> GetCustomers()
    {
        // This will automatically use local database if sync is active
        // and will sync from remote if needed
        string query = "SELECT Id, Name, Email FROM Customers";
        var results = ExecuteQuery(query);
        
        var customers = new List<Customer>();
        foreach (var row in results)
        {
            customers.Add(new Customer 
            { 
                Id = Convert.ToInt32(row["Id"]),
                Name = row["Name"].ToString(),
                Email = row["Email"].ToString()
            });
        }
        return customers;
    }
    
    public void ForceSync()
    {
        // Manually trigger synchronization
        SyncDatabaseFile();
    }
}

// Usage
var handler = new SyncedDatabaseHandler(
    remotePath: @"\\fileserver\databases\crm.db",
    localPath: @"C:\LocalData\crm_local.db"
);

// The handler will automatically sync when needed
var customers = handler.GetCustomers();

// Check sync status
Console.WriteLine($"Last sync: {handler.LastUpdate}");
Console.WriteLine($"Using local database: {handler.IsLocalSyncActive}");

// Force immediate sync if needed
if (handler.IsRemoteDatabaseOnline())
{
    handler.ForceSync();
}
```

### Example 3: Handling Network Connectivity Issues

This example demonstrates robust error handling and graceful degradation when network connectivity is unreliable.

**Practical Use Cases:**
- Field service applications with intermittent connectivity
- Remote office applications with unreliable VPN connections
- Applications that must continue operating during scheduled maintenance windows
- Disaster recovery scenarios where remote systems may be temporarily unavailable

```csharp
public class RobustDatabaseHandler : SQLiteDatabaseHandler
{
    public RobustDatabaseHandler(string remotePath, string localPath) 
        : base(remotePath, localPath, updateIntervalMin: 5)
    {
    }
    
    public void PerformDatabaseOperations()
    {
        try
        {
            // Check if we can work with local database
            if (IsLocalDatabaseDefined)
            {
                if (!IsRemoteDatabaseOnline())
                {
                    Console.WriteLine("Remote database offline. Using local copy.");
                }
                
                // Operations will use local database automatically
                var data = ExecuteQuery("SELECT COUNT(*) FROM Products");
                Console.WriteLine($"Product count: {data.Rows[0][0]}");
            }
            else
            {
                // No local sync configured, must use remote
                if (IsRemoteDatabaseOnline())
                {
                    var data = ExecuteQuery("SELECT COUNT(*) FROM Products");
                    Console.WriteLine($"Product count: {data.Rows[0][0]}");
                }
                else
                {
                    Console.WriteLine("Database unavailable and no local copy configured.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database operation failed: {ex.Message}");
        }
    }
    
    public void TemporarilyDisableSync()
    {
        // Suspend automatic syncing
        SuspendLocalSync = true;
        Console.WriteLine("Automatic sync suspended.");
    }
    
    public void ResumeSync()
    {
        // Resume automatic syncing
        SuspendLocalSync = false;
        Console.WriteLine("Automatic sync resumed.");
    }
}
```

## Best Practices

### 1. Error Handling
Always wrap database operations in try-catch blocks and check for remote database availability:

```csharp
if (handler.IsRemoteDatabaseOnline() || handler.IsLocalDatabaseDefined)
{
    try
    {
        // Perform database operations
    }
    catch (Exception ex)
    {
        // Handle database errors
    }
}
```

### 2. Sync Management
For applications that perform frequent database writes, consider temporarily suspending sync during bulk operations:

```csharp
handler.SuspendLocalSync = true;
// Perform bulk operations on remote database
handler.SuspendLocalSync = false;
```

### 3. Local Path Configuration
Ensure the local path is in a writable directory with sufficient disk space:

```csharp
string localPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyApp",
    "database_cache.db"
);
```

## Threading Considerations

- The synchronization process is not thread-safe by default
- If using in multi-threaded applications, implement appropriate locking mechanisms
- Consider the sync interval when designing concurrent access patterns

## Performance Notes

- Local database access is significantly faster than remote access
- Sync operations may take time depending on database size and network speed
- The default 10-minute sync interval balances performance with data freshness
- Consider your application's requirements when setting the update interval

## API Reference

## Namespace

```csharp
using CodeArtEng.SQLite;
```

## Class Declaration

```csharp
public abstract class SQLiteDatabaseHandler : SQLiteHelper
```

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `DatabaseFilePath` | `string` | Public | Gets the full path to the remote database file |
| `LocalFilePath` | `string` | Public | Gets the full path to the local synchronized copy |
| `IsLocalDatabaseDefined` | `bool` | Public | Returns `true` if a local database path is configured |
| `IsLocalSyncActive` | `bool` | Protected | Indicates if currently using the local synchronized database |
| `UpdateIntervalMinutes` | `int` | Protected | Sync interval in minutes (default: 10) |
| `LastUpdate` | `DateTime` | Protected | Timestamp of the last successful synchronization |
| `SuspendLocalSync` | `bool` | Protected | Temporarily suspends automatic synchronization |

### Constructors

| Constructor | Parameters | Description |
|-------------|------------|-------------|
| `SQLiteDatabaseHandler(string)` | `remotePath` | Creates handler for remote-only database access |
| `SQLiteDatabaseHandler(string, string, int)` | `remotePath`, `localPath`, `updateIntervalMin` | Creates handler with local sync capability |

### Public Methods

| Method | Return Type | Parameters | Description |
|--------|-------------|------------|-------------|
| `IsRemoteDatabaseOnline()` | `bool` | None | Checks if remote database file is accessible |
| `SyncDatabaseFile()` | `void` | None | Manually synchronizes database from remote to local |

### Protected Methods

| Method | Return Type | Parameters | Description |
|--------|-------------|------------|-------------|
| `SwitchToRemoteDatabase(bool)` | `void` | `readOnly` (default: false) | Switches connection to remote database |
| `SwitchToLocalDatabase()` | `void` | None | Switches connection to local synchronized copy |