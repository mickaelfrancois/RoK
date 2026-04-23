using System.Text;

namespace Rok.Infrastructure.Migration;

public class SqlBuilder
{
    public enum EColumnType { Text, Integer, Bool, DateTime, Float, Long }

    private StringBuilder _sql = new();

    private StringBuilder _keyPart = new();

    private string _currentTableName = string.Empty;

    private string _currentColumnName = string.Empty;




    public SqlBuilder CreateTable(string tableName)
    {
        Guard.Against.NullOrEmpty(tableName);

        _currentTableName = tableName;
        _currentColumnName = "";

        _keyPart = new StringBuilder();
        _sql = new StringBuilder();
        _sql.Append($"CREATE TABLE IF NOT EXISTS `{tableName}` (");


        return this;
    }


    public SqlBuilder WithIdColumn(string columnName)
    {
        Guard.Against.NullOrEmpty(columnName);

        _sql.Append($"{columnName} INTEGER NOT NULL CONSTRAINT PK_{_currentTableName} PRIMARY KEY AUTOINCREMENT ");

        _currentColumnName = columnName;

        return this;
    }


    public SqlBuilder WithColumn(string columnName)
    {
        Guard.Against.NullOrEmpty(columnName);

        _sql.Append($", {columnName} ");

        _currentColumnName = columnName;

        return this;
    }

    public SqlBuilder OfType(EColumnType columnType)
    {
        switch (columnType)
        {
            case EColumnType.Text:
                _sql.Append("TEXT ");
                break;
            case EColumnType.Integer:
                _sql.Append("INTEGER ");
                break;
            case EColumnType.Long:
                _sql.Append("LONG ");
                break;
            case EColumnType.Bool:
                _sql.Append("INTEGER ");
                break;
            case EColumnType.DateTime:
                _sql.Append("TEXT ");
                break;
            case EColumnType.Float:
                _sql.Append("REAL ");
                break;
        }

        return this;
    }

    public SqlBuilder WithDefaultValue(string defaultValue)
    {
        _sql.Append($"DEFAULT {defaultValue} ");
        return this;
    }

    public SqlBuilder AsNotNull(string defaultValue)
    {
        _sql.Append("NOT NULL ");
        WithDefaultValue(defaultValue);
        return this;
    }

    public SqlBuilder AsNotNull()
    {
        _sql.Append("NOT NULL ");
        return this;
    }

    public SqlBuilder AsNull()
    {
        _sql.Append("NULL ");
        return this;
    }


    public SqlBuilder AsKey()
    {
        _keyPart.Append($"CREATE INDEX IF NOT EXISTS Idx_{_currentTableName}_{_currentColumnName} ON {_currentTableName} ({_currentColumnName});");
        return this;
    }


    public SqlBuilder AsUniqueKey()
    {
        _keyPart.Append($"CREATE UNIQUE INDEX IF NOT EXISTS {_currentTableName}_{_currentColumnName} ON {_currentTableName} ({_currentColumnName});");
        return this;
    }


    public string ToSql()
    {
        _sql.Append(");");
        _sql.Append(_keyPart);
        return _sql.ToString();
    }
}
