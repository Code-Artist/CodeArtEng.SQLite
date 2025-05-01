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
	+ [Array Table](#array-table)
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
To update or write new data into database, you can utilize the `WriteToDatabase` method. 
Although it is possible perform the same action using `ExecuteNonQuery` method for simple table structure, `WriteToDatabase` method capable to handle more complex database structure which described in following section.
```C#
public void WriteEmployeeData(Employee[] newDatas)
{
    WriteToDatabase(newDatas);
}
```

# Reading and Writing Complex Tables (ORM)
The `ReadFromDatabase` and `WriteToDatabase` methods make it easy to link objects in your code to tables in your database. They work well with tables that have relationships (child tables) and can handle working with more than one database using simple commands. Let’s take a closer look at what they can do.

These methods follow the Fail Fast Principle, which means they quickly check if the structure of your objects matches the structure of your database tables when you first use them. This check is to make sure that all the columns match up. To avoid problems with older versions, your database tables can have extra columns that aren’t in your objects, but not the other way around.

## SQLite Write Option
The 'WriteOptions' properties specify by `SQLiteWriteOption` class, sets how SQLiteHelper behaves when reading and writing data with following options:

* `CreateTable`: Automatic create table in database if not exists when set to true. Do nothing if table already exists.
* `WriteMode`: Used by `WriteToDatabase` method to decide what to update. (Reserved for future implemntation, not available yet)

## Table Name
Mapping a class to database table named *Employee*. Use `SQLName` attribute to overwrite default table name.
```C#
public class Employee { ... }

[SQLName("Employee")]
public class Emp { ... }
```    

## Column Name
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

## Data Type
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

## Index Table
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
  |- Name, TEXT, Unique
```

## Primary Key
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

## Parent and Child Tables

When working with relational data models, parent and child table relationships are commonly used to represent hierarchical or associated data. These relationships are often defined using key attributes and can follow either a **one-to-many** or **one-to-one** mapping, depending on the structure and design of the data.

### One-to-Many Relationship

In a **one-to-many** relationship, a single record in the parent table can be associated with multiple records in the child table. For example, the `Department` table serves as a **parent**, and the `Employee` table acts as the **child**, where each department can have multiple employees. `Employee` class may have primary key included if required.

```csharp
public class Department
{
    [PrimaryKey]
    public int ID { get; set; }
    public string Name { get; set; }
    public List<Employee> Employees { get; set; } = new List<Employee>();
}

public class Employee
{
    public string Name { get; set; }

    [ParentKey(typeof(Department))]
    public int DepartmentID { get; set; }
}
```

Equivalent database structure:

```
Department (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT

Employee (Table)
  |- Name, TEXT
  |- DepartmentID, INTEGER  
```

In this model, the `DepartmentID` in the `Employee` table acts as a foreign key, linking each employee to their respective department. The `[ParentKey]` attribute ensures the proper mapping between the child and parent records.

### One-to-One Relationship

In a **one-to-one** relationship, each record in the parent table is associated with exactly one record in the child table. In such cases, instead of using a `List<T>` for the child, a **single object** can be used. For example, a `Department` might have exactly one `Location`. This child record can also be optional—for instance, a department may or may not have an assigned location. In one-to-one relationship, parent and child table matching using `PrimaryKey`.

```csharp
public class Department
{
    [PrimaryKey]
    public int ID { get; set; }
    public string Name { get; set; }
    public Location Location { get; set; }
}

public class Location
{
    public string Address { get; set; }

    [PrimaryKey]
    public int DepartmentID { get; set; }
}
```

Corresponding database structure:

```
Department (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT

Location (Table)
  |- Address, TEXT
  |- DepartmentID, INTEGER  
```

Here, each department is linked to a single location through the `DepartmentID`, using a one-to-one mapping. This approach is useful when the related child entity is unique per parent and should be treated as a single nested object rather than a collection.

### Summary

- Use `List<Child>` when modeling **one-to-many** relationships.
- Use a **single object** for **one-to-one** relationships.
- The child table must include a property decorated with `[ParentKey(typeof(Parent))]` for one-to-many relationship while `[PrimaryKey]` property is expected for child table with one-to-one relationship.
- The parent class must have a `[PrimaryKey]` defined for mapping to work correctly.

This flexible design pattern allows you to model different types of relationships clearly and efficiently using object-oriented principles with SQLite helpers.

## Multiple Database Source
SQLite Helper also supports multiple database source, allow data to be read and write from tables stored in different SQLite database files. Example below showing that `Department` table is stored in main database while `Employee` table is table stored in **Employee.db**. Switching between main and sub database are handled internally by read and write method.
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

## Array Table
The array table functionality allows the storage of array properties from a sample table, `TableWithArray`, into separate SQLite tables. This process involves creating specific SQLite tables for each type of array property, enabling efficient storage and retrieval of array data. The following example demonstrates how to map the array properties into SQLite tables.

```C#
public class TableWithArray
{
	[PrimaryKey]
	public int ID { get; set; }

	...
	
	public string[] ArrayData { get; set; }

	[SQLName("ArrayIntValue")]
	public int[] ItemValue { get; set; }
}

```
`ArrayData`is a string array which mapped into ArrayTable with TEXT value.
```
ArrayData (Table)
  |- ID, INTEGER
  |- Value, TEXT
```

`ItemValue` is an integer array, mapped to the SQLite table `ArrayIntValue` using the SQLName attribute.
```
ArrayIntValue (Table)
  |- ID, INTEGER
  |- Value, INTEGER
```

## Unique Constraint
The `SQLUnique` and `SQLUniqueMultiColumn` attributes are used to mark columns with a unique constraint. `SQLUnique` sets a unique constraint on a single column, while `SQLUniqueMultiColumn` sets unique constraints across multiple columns.

Example usage of these attributes as follow:
```C#
public class User
{
    [PrimaryKey]
    public int ID { get; set; }

    [SQLUnique]
    public string Email { get; set; }

    public string FirstName { get; set; }
    public string LastName { get; set; }

    [SQLUniqueMultiColumn]
    public string Username { get; set; }

    [SQLUniqueMultiColumn]
    public string PhoneNumber { get; set; }
}
```
SQL Table Structure
```
User (Table)
  |- ID, INTEGER, Primary Key
  |- Email, TEXT, Unique
  |- FirstName, TEXT
  |- LastName, TEXT
  |- Username, TEXT
  |- PhoneNumber, TEXT
     (Unique Username, PhoneNumber)	 
```
SQL Schema
```SQL
CREATE TABLE User (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Email TEXT UNIQUE,
    FirstName TEXT,
    LastName TEXT,
    Username TEXT,
    PhoneNumber TEXT,
    UNIQUE (Username, PhoneNumber)
);
```

In this example:

The `Email` column is marked with the `SQLUnique` attribute, ensuring that each email address is unique.
The `Username` and `PhoneNumber` columns are marked with the `SQLUniqueMultiColumn` attribute, ensuring that the combination of `Username` and `PhoneNumber` is unique across the table.


# Suggestion and Feedback
We hope this document has provided you with clear and helpful information to use this tool.
Your feedback is invaluable to us as it helps improve the quality of our work and clarity of our documentation. Please share your suggestions, comments, or any difficulties you encountered while using this guide. Your input will assist us in enhancing our resources and supporting users like you more effectively. Thank you for your attention and contribution.

# Additional Resources
For a wide range of powerful development tools, visit our homepage at www.codearteng.com. Explore utilities designed to streamline your workflow, boost productivity, and tackle diverse programming challenges efficiently.