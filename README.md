- [SQLite Helper: A Micro-ORM for SQLite Database](#sqlite-helper--a-micro-orm-for-sqlite-database)
  * [Dependency](#dependency)
  * [Key Features of SQLite Helper](#key-features-of-sqlite-helper)
  * [Anatomy of SQLiteHelper](#anatomy-of-sqlitehelper)
- [Using SQLiteHelper](#using-sqlitehelper)
  * [Create SQLite Database class](#create-sqlite-database-class)
  * [Read from Database Table](#read-from-database-table)
  * [Update Data to Database](#update-data-to-database)
- [ORM](#orm)
  * [Table Name](#table-name)
  * [Column Name](#column-name)
  * [Data Type](#data-type)
  * [Index Table](#index-table)
  * [Primary Key](#primary-key)
  * [Child Table](#child-table)
  * [Multiple Database Source](#multiple-database-source)

# SQLite Helper: A Micro-ORM for SQLite Database
SSQLite Helper is a micro-Object-Relational Mapping (ORM) tool crafted to facilitate application development with SQLite databases. It is particularly well-suited for small to medium-scale applications, negating the necessity of authoring each SQL query from the ground up.

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

In summary, `SQLiteHelper` stands as an invaluable asset for developers engaging with SQLite databases. It significantly streamlines database interactions, thereby enabling developers to allocate greater focus on application logic rather than on the intricacies of SQL query construction. `SQLiteHelper` is adept at enhancing the development workflow for both small-scale applications and medium-sized projects alike.In summary, `SQLiteHelper` stands as an invaluable asset for developers engaging with SQLite databases. It significantly streamlines database interactions, thereby enabling developers to allocate greater focus on application logic rather than on the intricacies of SQL query construction. `SQLiteHelper` is adept at enhancing the development workflow for both small-scale applications and medium-sized projects alike.

## Anatomy of SQLiteHelper
* **SQLiteHelper** *(abstract)*: Primary class for the package. It encompasses all methods necessary for database read and write operations.
* **SQLiteDatabaseHandler** *(abstract)*: This is a subclass derived from `SQLiteHelper`. It inherits all features from the SQLiteHelper class and additionally has the ability to toggle between remote and local databases, as well as synchronize data from a remote source to a local cached copy.
* **SQLiteDataReaderEx** *(extension)*: Extension class for `SQLiteDataReader` which handle `null` check for get value method.
* **SQLAttribute**: Attribute base class for table mapping.

# Using SQLiteHelper
## Create SQLite Database class
Create project specific database class inherite from `SQLiteHelper` class.
```C#
public class MyDatabase : SQLiteHelper
{
    public MyDatabase(string databaseFilePath): base()
    {
        SetSQLPath(databaseFilePath);
    }
}
```

## Read from Database Table
To read data from Table Employee with following columns:
```
Employee (Table)
  |- ID, INTEGER, Primary Key
  |- Name, TEXT
  |- Department, TEXT
  |- Salary, INTEGER
```

Store into data class named Employee as follow:
```C#
public class Employee
{
    public int ID {get; set;}
    public string Name {get; set;}
    public string Department {get; set;}
    public int Salary {get; set;}
}
```

1. Read data with `ExecuteQuery` method:
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

## Update Data to Database
To update data in a database, you can utilize the `WriteToDatabase` method. Alternatively, achieving the same results is possible by employing the `ExecuteNonQuery` method.
```C#
public void WriteEmployeeData(Employee[] newDatas)
{
    WriteToDatabase(newDatas);
}
```

# ORM
The `ReadFromDatabase` and `WriteToDatabase` methods implement object-to-database mapping. They are designed to work with child tables and support multiple database sources using an easy-to-understand syntax. To get started, let's walk through some basic rules and assumption.

These 2 methods are designed with [Fail Fast Principal](https://www.codereliant.io/fail-fast-pattern/). Object mapping are compared with database structure for main and child tables on first execution call to either one of this method to ensure all the columns are matching. To maintain backward compatibility, we allowed database table contains more columns than the mapped object, but not the other way round.

## Table Name
Mapping a class to database table named *Employee*.
```C#
public class Employee { ... }

[SQLName("Employee")]
public class Emp { ... }
```    

## Column Name
All public properties with public getters and setters are treated as SQL columns. By default, the property names are used as column names.
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
**NOTE**: SQLite automatically convert values to the appropriate datatype. More details in SQLite documentation [Type Affinity](https://sqlite.org/datatype3.html#type_affinity)

## Data Type
Table below show default data type mapping object and database.

| Object Type | Database Type |
|----|----|
| string, enum, DateTime | TEXT
| int, double, long, decimal, float | INTEGER, NUMERIC |

Enum and DateTime values can be stored as integer in Database by adding `SQLDataType` attribute. Enum `Status` stored as integer while `DateTime` stored as ticks (long).
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
The following example illustrates that `UserName` is stored as an index in column named `NameID` of `Employee` table , while the string value is stored in a key-value pair table named `Name`. This setup allows for efficient retrieval and management of data especially when same name is used multiple time in different tables.

Table name parameter for `SQLIndexTable` is optional, property name will be used as table name if leave blank.

```C#
public class Employee
{
    [SQLIndexTable("Name")]
    [SQLName(""NameID")]
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

## Primary Key
The primary key attribute corresponds to the primary key in the database table.
When you execute the `WriteToDatabase` method with an item where the ID is equal to 0, a new entry will be added to the database table with the next assigned unique ID. Otherwise, a row with matching ID will be updated.
```C#
public class Employee
{
    [PrimaryKey]
    public int ID {get; set;}
    ...
}
```

## Child Table
Consider the following example, list of object `List<Employee>` is treated as child table with one to many relation. Property name `Employees` shall be used as Table name for child table unless explicitly specified by `SQLName` attribute which stated table name as `Employee` as shown below.
```C#
public class Department
{
    [PrimaryKey]
    public int ID { get; set; }
    public string Name { get; set; }
    [SQLName("Employee")]
    public List<Employee> Employees { get; set; } = new List<Employee>();
    ...
}

public class Employee
{
    [PrimaryKey]
    public int ID { get; set; }
    [SQLIndexTable]
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
  |- ID, INTEGER, Primary Key
  |- Name, TEXT
  |- DepartmentID, INTEGER  
```
`DepartmentID` stored the `ID` of `Department` table for respective row.

## Multiple Database Source
SQLiteHelper support multiple database source, allow data to read from tables stored in different SQLite database file. Example below showing that `Department` table is stored in main database while `Employee` table is table stored in **Employee.db**. Switching between main and sub database are handled internally by read and write method.
```C#
public class Department
{
    ...
    [SQLName("Employee")]
    [SQLDatabase("Employee.db")]
    public List<Employee> Employees { get; set; } = new List<Employee>();
}
```
