# SQLiteHelper: A Micro-ORM for SQLite Database
## Introduction
SQLiteHelper is a micro-ORM (Object-Relational Mapping) designed to simplify application development with SQLite databases. It is particularly suitable for small to medium scale applications, eliminating the need to write every single SQL query from scratch.
It’s important to note that SQLiteHelper is not intended to replace Entity Framework. While Entity Framework is a robust ORM with a full set of features, SQLiteHelper is designed with simplicity and speed in mind. It provides a streamlined interaction with SQLite databases through simple functions.

## Key Features of SQLiteHelper
SQLiteHelper comes with a set of features aimed at making your interaction with SQLite databases as smooth as possible:

1. **Manage Connection String:** With SQLiteHelper, you only need to provide the file path to the database file. It takes care of the rest.
2. **Automatic Open and Close Connection:** SQLiteHelper handles the connection to the database with a helper class. This means you no longer need to keep track of the connection status or worry about forgetting to release the database after a write operation.
3. **Object Mapping to Database Class:** SQLiteHelper allows you to perform read and write operations from the database with a single method call. It maps objects to the database class, simplifying the process of data manipulation.
4. **Handle Queries from Different Database Sources:** SQLiteHelper is capable of handling queries from different database sources, providing you with the flexibility you need when working with multiple databases.

In conclusion, SQLiteHelper is a powerful tool for developers working with SQLite databases. It simplifies the process of database interaction, allowing developers to focus more on the application logic and less on writing SQL queries. Whether you’re developing a small application or a medium-sized project, SQLiteHelper can help streamline your development process.

## Anatomy of SQLiteHelper
* **SQLiteHelper** *(abstract)*: This is the primary helper class for the SQLite database. It encompasses all methods for reading from and writing to the database.
* **SQLiteDatabaseHandler** *(abstract)*: This is a subclass derived from SQLiteHelper. It inherits all features from the SQLiteHelper class and additionally has the ability to toggle between remote and local databases, as well as synchronize data from a remote source to a local cached copy.
* **SQLiteDataReaderEx** *(extension)*: Extension class for `SQLiteDataReader` which handle `null` check for get value method.
* **SQLAttribute**: Attribute base class for table mapping.

