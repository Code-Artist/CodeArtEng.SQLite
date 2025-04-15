using System;
using NUnit.Framework;

namespace CodeArtEng.SQLite.Tests
{
    [TestFixture]
    public class SqlParameterConverterTests
    {
        [Test]
        public void NullWhereClause_ReturnsEmpty()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters(null);
            Assert.That(result.ProcessedWhere, Is.EqualTo(string.Empty));
            Assert.That(result.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void EmptyWhereClause_ReturnsEmpty()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters(string.Empty);
            Assert.That(result.ProcessedWhere, Is.EqualTo(string.Empty));
            Assert.That(result.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void SimpleStringEquality_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Name = 'Jo,hn '");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Name = @param1"));
            Assert.That(result.Parameters.Count, Is.EqualTo(1));
            Assert.That(result.Parameters[0].ParameterName, Is.EqualTo("@param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo("Jo,hn "));
        }

        [Test]
        public void ComplexStringEquality_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Name = 'Jo hn' AND Place = ' School '");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Name = @param1 AND Place = @param2"));
            Assert.That(result.Parameters.Count, Is.EqualTo(2));
            Assert.That(result.Parameters[0].ParameterName, Is.EqualTo("@param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo("Jo hn"));
            Assert.That(result.Parameters[1].ParameterName, Is.EqualTo("@param2"));
            Assert.That(result.Parameters[1].Value, Is.EqualTo(" School "));
        }

        [Test]
        public void SimpleNumericEquality_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Age = 30");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Age = @param1"));
            Assert.That(result.Parameters.Count, Is.EqualTo(1));
            Assert.That(result.Parameters[0].ParameterName, Is.EqualTo("@param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(30m));
        }

        [Test]
        public void FloatingPointValue_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Price = 45.99");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Price = @param1"));
            Assert.That(result.Parameters.Count, Is.EqualTo(1));
            Assert.That(result.Parameters[0].ParameterName, Is.EqualTo("@param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(45.99m));
        }

        [Test]
        public void NullValue_ConvertsToDbNull()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("LastName = NULL");
            Assert.That(result.ProcessedWhere, Is.EqualTo("LastName = @param1"));
            Assert.That(result.Parameters.Count, Is.EqualTo(1));
            Assert.That(result.Parameters[0].ParameterName, Is.EqualTo("@param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(DBNull.Value));
        }

        [Test]
        public void NotEqualOperator_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Age != 25");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Age != @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(25m));
        }

        [Test]
        public void GreaterThanOperator_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Price > 100");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Price > @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(100m));
        }

        [Test]
        public void LessThanOrEqualOperator_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Quantity <= 5");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Quantity <= @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(5m));
        }

        [Test]
        public void NotEqualAlternativeSyntax_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Status <> 'Pending'");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Status <> @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo("Pending"));
        }

        [Test]
        public void MultipleConditions_ConvertsAllParameters()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("FirstName = 'Jane' AND Age >= 21 AND Status != 'Inactive'");
            Assert.That(result.ProcessedWhere, Is.EqualTo("FirstName = @param1 AND Age >= @param2 AND Status != @param3"));
            Assert.That(result.Parameters.Count, Is.EqualTo(3));
            Assert.That(result.Parameters[0].Value, Is.EqualTo("Jane"));
            Assert.That(result.Parameters[1].Value, Is.EqualTo(21m));
            Assert.That(result.Parameters[2].Value, Is.EqualTo("Inactive"));
        }

        [Test]
        public void StringWithSpecialCharacters_PreservesCharacters()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Comment = 'This is a test with spaces and special chars: !@#$%^&*()'");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Comment = @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo("This is a test with spaces and special chars: !@#$%^&*()"));
        }

        [Test]
        public void EmptyStringValue_ConvertsCorrectly()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("Notes = ''");
            Assert.That(result.ProcessedWhere, Is.EqualTo("Notes = @param1"));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(""));
        }

        [Test]
        public void WhereClauseWithNoValues_ReturnsOriginalClause()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("IsActive AND IsVerified");
            Assert.That(result.ProcessedWhere, Is.EqualTo("IsActive AND IsVerified"));
            Assert.That(result.Parameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void ComplexConditions_ConvertsAllParameters()
        {
            var result = SQLiteParameterConverter.ConvertWhereToParameters("(Age > 18 AND Status = 'Active') OR (Age = 18 AND ParentalConsent = 'Yes')");
            Assert.That(result.ProcessedWhere, Is.EqualTo("(Age > @param1 AND Status = @param2) OR (Age = @param3 AND ParentalConsent = @param4)"));
            Assert.That(result.Parameters.Count, Is.EqualTo(4));
            Assert.That(result.Parameters[0].Value, Is.EqualTo(18m));
            Assert.That(result.Parameters[1].Value, Is.EqualTo("Active"));
            Assert.That(result.Parameters[2].Value, Is.EqualTo(18m));
            Assert.That(result.Parameters[3].Value, Is.EqualTo("Yes"));
        }

        [Test]
        public void InvalidOperator_ThrowsSqlInjectionException()
        {
            Assert.Throws<SQLInjectionException>(() =>
                SQLiteParameterConverter.ConvertWhereToParameters("Name = 'John' OR 1=1 --"));
        }

        [Test]
        public void InvalidNumericValue_ThrowsSqlInjectionException()
        {
            Assert.Throws<SQLInjectionException>(() =>
                SQLiteParameterConverter.ConvertWhereToParameters("Age = 25abc"));
        }
    }
}