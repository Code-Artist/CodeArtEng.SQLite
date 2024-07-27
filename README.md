# SQLite Helper: A Micro-ORM for SQLite Database
![NuGet Version](https://img.shields.io/nuget/v/codearteng.sqlite)
![NuGet Downloads](https://img.shields.io/nuget/dt/codearteng.sqlite)

- [Introduction](#introduction)
  * [Dependency](#dependency)
  * [Key Features of SQLite Helper](#key-features-of-sqlite-helper)
  * [Anatomy of SQLiteHelper](#anatomy-of-sqlitehelper)
- [Using SQLiteHelper](#using-sqlitehelper)
  * [Create SQLite Database class](#create-sqlite-database-class)
  * [Basic Query Functions](#basic-query-functions)
  * [Read from Database Table](#read-from-database-table)
  * [Write Data to Database](#write-data-to-database)
  * [Reading and Writing Complex Tables (ORM)](#reading-and-writing-complex-tables--orm-)
    + [Table Name](#table-name)
    + [Column Name](#column-name)
    + [Data Type](#data-type)
    + [Index Table](#index-table)
    + [Primary Key](#primary-key)
    + [Parent and Child Tables](#parent-and-child-tables)
    + [Multiple Database Source](#multiple-database-source)
- [Suggestion and Feedback](#suggestion-and-feedback)

# Introduction
SQLite Helper is a micro-Object-Relational Mapping (ORM) tool crafted to facilitate application development with SQLite databases. It is particularly well-suited for small to medium-scale applications, negating the necessity of authoring each SQL query from the ground up.

Conversely, Entity Framework (EF) is a comprehensive ORM offering a complete suite of functionalities. However, more features do not inherently equate to superiority. It is prudent to weigh the [Pros and Cons of EF](https://www.codearteng.com/2024/04/entity-framework-advantages-and.html) prior to its adoption.

While Entity Framework presents a robust ORM solution with an extensive feature set, SQLite Helper is tailored for simplicity and expediency, enabling streamlined interactions with SQLite databases through straightforward functions.

An article [Micro ORM vs ORM](https://yaplex.com/micro-orm-vs-orm/) written by Alex Shapovalov explained in details difference between Micro ORM vs ORM and how to choose among them.

## Dependency
* NuGet: [System.Data.SQLite.Core](https://www.nuget.org/packages/System.Data.SQLite.Core)
* .NET Framework 4.8

## Key Features of SQLite Helper
SQLite Helper comes with a set of features aimed at making your interaction with SQLite databases as smooth as possible:

1. **Manage Connection String:** `SQLiteHelper` requires only the database file path to establish a connection, streamlining the process significantly.
2. **Automatic Open and Close Connection:** `SQLiteHelper` employs a helper class to manage database connections, obviating the need for manual tracking of connection states and ensuring proper release post-write operations.
3. **Object Mapping to Database Class:** `SQLiteHelper` facilitates single-method read and write operations to the database by mapping objects directly to the database class, thereby simplifying data manipulation tasks.
4. **Handle Queries from Different Database Sources:** `SQLiteHelper` is adept at processing queries from various database sources, offering the requisite flexibility for managing multiple databases.
5. **Utility Functions:** `SQLiteHelper` includes utility methods such as `ClearTable`, `GetPrimaryKeys`, `GetTableSchema`, among others, all designed with safeguards against SQL injection—a common oversight for novices.

In conclusion, `SQLiteHelper` is an essential tool for developers working with SQLite databases. It simplifies database interactions, allowing developers to concentrate more on the application's logic rather than the complexities of SQL query writing. `SQLiteHelper` is effective in improving the development process for both small and medium-sized projects.

## Anatomy of SQLiteHelper
* **SQLiteHelper** *(abstract)*: Primary class for the package. It encompasses all methods necessary for database read and write operations.
* **SQLiteDatabaseHandler** *(abstract)*: This is a subclass derived from `SQLiteHelper`. It inherits all features from the SQLiteHelper class and additionally has the ability to toggle between remote and local databases, as well as synchronize data from a remote source to a local cached copy.
* **SQLiteDataReaderEx** *(extension)*: Extension class for `SQLiteDataReader` which handle `null` check for get value method.
* **SQLAttribute**: Attribute base class for table mapping.

# Using SQLiteHelper
As a helper class, most methods within `SQLiteHelper` are designated as protected, as it is not anticipated that users of derived classes will interact directly with the database layer. All operations related to the database should remain concealed from the user at both the API and application levels.

## Create SQLite Database class
Create project specific database class inherits from `SQLiteHelper` class.
`SetSQLPath` have an optional `readOnly` parameter when set to true will open the database file in read only mode.
```C#
public class MyDatabase : SQLiteHelper
{
    public MyDatabase(string databaseFilePath): base()
    {
        SetSQLPath(databaseFilePath);
    }
}
```

### Handling Busy Connection
Proper handling of retry and timeout is crucial to ensure transaction completed successfully without impact user experience.
Parameters `SQLStepRetries` and `SQLBusyTimeout` are 2 important parameters to define how many iteration and delay used to retry for busy (locked database) connection.

## Basic Query Functions
In SQLite Helper, we offer a suite of query methods that internally handle database connections, eliminating the need to search for unclosed connections that could result in a locked database.
```C#
protected void ExecuteQuery(string query, Action<SQLiteDataReader> processQueryResults)
protected object ExecuteScalar(string query)
protected int ExecuteNonQuery(string query)
protected void ExecuteTransaction(Action performTransactions)
```
Example below show usage of `ExecuteQuery` method. Connection are closed and `SQLiteDataReader` object `r` is disposed at end of `ExecuteQuery` method.
```C#
  ExecuteQuery(query, (r)=>
  {
      while(r.Read())
      {
          //ToDo: Perform readback here...
      }
  });

```

## Read from Database Table
SQLite Helper offer 2 solutions to read data from Employee table into `Employee` class object as shown below:
```
Employee (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT
  |- Department, TEXT
  |- Salary, INTEGER
```

```C#
public class Employee
{
    public int ID {get; set;}
    public string Name {get; set;}
    public string Department {get; set;}
    public int Salary {get; set;}
}
```

1. The convention way, manually read data from database using `ExecuteQuery` method:
```C#
public Employee[] ReadEmployeeData()
{
    List<Employee> results = new List<Employee>();
    //Execute Query handle database connection
    ExecuteQuery("SELECT * FROM Employee", (r) =>
    {
        //(r) = Delegate call back function with SQLiteDataReader parameter r.
        //Disposal of r is taken care by ExecuteQuery method.
        int x;
        while(r.Read())
        {
            x = 0;
            Employee e = new Employee();
            e.ID = r.GetInt32(x++);
            e.Name = r.GetStringEx(x++);  //Extension method. Handle null value.
            e.Department = r.GetStringEx(x++);
            e.Salary = r.GetInt32Ex(x++);z
        }
    });
}
```

2. Implementation above can be further simplify using query with class - `ReadFromDatabase`
```C#
public Employee[] ReadEmployeeData()
{
    return ReadFromDatabase<Employee>().ToArray();
}
```

## Write Data to Database
To update or write new data into database, you can utilize the `WriteToDatabase` method. Alternatively, achieving the same results is possible by employing the `ExecuteNonQuery` method.
```C#
public void WriteEmployeeData(Employee[] newDatas)
{
    WriteToDatabase(newDatas);
}
```

## Reading and Writing Complex Tables (ORM)
The ReadFromDatabase and WriteToDatabase methods make it easy to link objects in your code to tables in your database. They work well with tables that have relationships (child tables) and can handle working with more than one database using simple commands. Let’s take a closer look at what they can do.

These methods follow the Fail Fast Principle, which means they quickly check if the structure of your objects matches the structure of your database tables when you first use them. This check is to make sure that all the columns match up. To avoid problems with older versions, your database tables can have extra columns that aren’t in your objects, but not the other way around.

### Table Name
Mapping a class to database table named *Employee*. Use `SQLName` attribute to overwrite default table name.
```C#
public class Employee { ... }

[SQLName("Employee")]
public class Emp { ... }
```    

### Column Name
All public properties that have public getters and setters are regarded as SQL columns.
The names of these properties are, by default, used as the names of the corresponding columns.
The `SQLName` attribute can be used to overwrite the default column name or table name.

```C#
public class Employee
{
    //Database Column: Name = 'Name', Type = TEXT
    public string Name {get; set;}

    //Database Column: Name = 'Department', Type = TEXT
    [SQLName("Department")]
    public string Dept {get; set;}

    //Database Column: Name = 'Salary', Type = INTEGER
    public int Salary {get; set;}

    //Database Column: Name = 'Cost', Type = NUMERIC
    public double Cost {get; set;}

    //Read only property is not a valid SQL Column
    public int Age {get;}
}
```

### Data Type
The table below displays the default data type mappings between objects and the database. Ensuring matching data types is crucial for the accurate writing and reading of data.
**NOTE**: SQLite may automatically convert values to the appropriate datatype. More details in SQLite documentation [Type Affinity](https://sqlite.org/datatype3.html#type_affinity)

| Object Type | Database Type |
|----|----|
| string, Enum, DateTime | TEXT |
| int,  long, bool  | INTEGER |
| double, decimal, float | REAL |

The `SQLDataType` attribute can be utilized to explicitly define the storage type of a value in the database. For instance, `Enum` and `DateTime` types can be stored as integers by applying the `SQLDataType` attribute, with Enum `Status` being stored as an integer and `DateTime` being stored as ticks (long).

```C#
public enum Status { ... }
public class MyTable
{
    [SQLDataType(DataType.INTEGER)]
    public Status CurrentStatus {get; set;}

    [SQLDataType(DataType.INTEGER)]
    public DateTime LastUpdate {get; set;}
}
```

### Index Table
The example below demonstrates that `UserName` is stored as an index in the `NameID` column of the `Employee` table, while the actual string value is kept in a key-value pair table named `Name`. This method facilitates efficient data retrieval and management, particularly when the same name is used multiple times across different tables.

Table name parameter for `SQLIndexTable` is optional. If left blank, the property name `UserName` will be used as the table name. The values for the index table can be shared among multiple tables.

```C#
public class Employee
{
    [SQLIndexTable("Name")]
    [SQLName("NameID")]
    public string UserName {get; set;}
}
```

```
Employee (Table)
  |- NameID, INTEGER
  | ...

Name (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT
```

### Primary Key
The primary key attribute is linked to the primary key in the database table. When the `WriteToDatabase` method is executed with an item whose ID is 0, it will create a new entry in the database table and assign it a unique ID. If the ID is not 0, it will update the existing row with the matching ID.
**NOTE:** Primary key must be declared with `int` type.
```C#
public class Employee
{
    [PrimaryKey]
    public int ID {get; set;}
    ...
}
```

### Parent and Child Tables
Let's examine the example provided: In database, `Department` (Table Name: Department) serves as a parent table, and `List<Employee>` (Table Name: Employees) functions as a child table with a one-to-many relationship, where each department can be associated with multiple employees. In other words, for every single entry in the `Department` table, there can be several corresponding entries in the `Employee` table, each representing an individual employee belonging to that department, while each `Employee` is assigned to only one `Department`.

Child table must have a properties ID declared with `ParentKey` attribute which function as mapping between child and parent table. Value of `DepartmentID` in example below is assigned by SQLite Helper. `PrimaryKey` for class class `Department` is mandatory while it is optional for class `Employee` depends on need of the design.

A child table must have an ID property, decorated with `ParentKey` attribute, which serves as the link between  child and parent table. In the example below, parent key value `DepartmentID` is assigned by SQLite Helper.

```C#
public class Department
{
    [PrimaryKey]
    public int ID { get; set; }
    public string Name { get; set; }
    public List<Employee> Employees { get; set; } = new List<Employee>();
    ...
}

public class Employee
{
    public string Name { get; set; }
    [ParentKey(typeof(Department))]
    public int DepartmentID { get; set; }
    ...
}
```
Equivalent database table are given as follow:
```
Department (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT

Employee (Table)
  |- Name, TEXT
  |- DepartmentID, INTEGER  
```

### Multiple Database Source
SQLite Helper also support multiple database source, allow data to be read and write from tables stored in different SQLite database files. Example below showing that `Department` table is stored in main database while `Employee` table is table stored in **Employee.db**. Switching between main and sub database are handled internally by read and write method.
This `SQLDatabase` attribute can only be used with child table.
```C#
public class Department
{
    ...
    [SQLName("Employee")]
    [SQLDatabase("Employee.db")]
    public List<Employee> Employees { get; set; } = new List<Employee>();
}
```

# Suggestion and Feedback
We hope this document has provided you with clear and helpful information to use this tool.
Your feedback is invaluable to us as it helps improve the quality of our work and clarity of our documentation. Please share your suggestions, comments, or any difficulties you encountered while using this guide. Your input will assist us in enhancing our resources and supporting users like you more effectively. Thank you for your attention and contribution.
