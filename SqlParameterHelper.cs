using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Project.Models
{
	public enum Direction
	{
		Input = 1,
		Output = 2,
		InputOutput = 3,
		ReturnValue = 6
	}
	public class StoredProcedureNameAttribute : Attribute
	{
		public string Name { get; set; }
		public StoredProcedureNameAttribute(string name)
		{
			Name = name;
		}
	}
	/// <summary>
	/// Use for an alternative param name other than the propery name
	/// </summary>
	[System.AttributeUsage(AttributeTargets.Property)]
	public class QueryParamAttribute : Attribute
	{
		public string _name { get; set; }
		public Direction _direction { get; set; }
		public bool _isStructured { get; set; }
		public string _dataType { get; set; }
		public object _dbType { get; set; }
		public int _size { get; set; }
		public byte _precision { get; set; }
		public byte _scale { get; set; }
		public QueryParamAttribute(string name = null, Direction direction = Direction.Input, string dataType = null, object dbType = null, int size = 0, byte precision = 0, byte scale = 0)
		{
			_name = name; // (name != "" ? name : "");
			_direction = direction;
			_dataType = dataType;
			_isStructured = (dataType != null);
			_dbType = dbType;
			_size = size;
			_precision = precision;
			_scale = scale;
		}
	}

	/// <summary>
	/// Ignore this property
	/// </summary>
	[System.AttributeUsage(AttributeTargets.Property)]
	public class QueryParamIgnoreAttribute : Attribute
	{
	}

	public class SqlParameterExtensions
	{
		private class QueryParamInfo
		{
			public string Name { get; set; }
			public object Value { get; set; }
		}

		public string GetStoredProcedureName(object obj)
		{
			StoredProcedureNameAttribute spnameAttribute = obj.GetType().GetCustomAttributes(
		typeof(StoredProcedureNameAttribute), true
	).FirstOrDefault() as StoredProcedureNameAttribute;
			if (spnameAttribute != null)
			{
				return spnameAttribute.Name;
			}
			throw
				new ApplicationException("Stored procedure name not declare to the class.");
		}

		public List<SqlParameter> ToSqlParamsList(object obj, SqlParameter[] additionalParams = null)
		{
			var props = (
				from p in obj.GetType().GetProperties()
				let nameAttr = p.GetCustomAttributes(typeof(QueryParamAttribute), true)
				let ignoreAttr = p.GetCustomAttributes(typeof(QueryParamIgnoreAttribute), true)
				select new { Property = p, Names = nameAttr, Ignores = ignoreAttr }).ToList();

			List<SqlParameter> result = new List<SqlParameter>();

			props.ForEach(p =>
			{
				if (p.Ignores != null && p.Ignores.Length > 0)
				{
					return;
				}

				QueryParamAttribute name = p.Names.FirstOrDefault() as QueryParamAttribute;
				if (name == null)
				{
					return;
				}

				QueryParamInfo pinfo = new QueryParamInfo();

				if (name != null && !string.IsNullOrWhiteSpace(name._name))
				{
					pinfo.Name = name._name.Replace("@", "");
				}
				else
				{
					pinfo.Name = p.Property.Name.Replace("@", "");
				}

				pinfo.Value = p.Property.GetValue(obj) ?? DBNull.Value;

				if (name._isStructured)
				{
					object data = p.Property.GetValue(obj) ?? DBNull.Value;
					List<dynamic> dlist = new List<dynamic>();
					string json = JsonConvert.SerializeObject(data);
					pinfo.Value = (DataTable)JsonConvert.DeserializeObject(json, (typeof(DataTable)));

					SqlParameter sqlParam = new SqlParameter(pinfo.Name, SqlDbType.Structured)
					{
						TypeName = name._dataType,
						Value = pinfo.Value,
						Direction = (ParameterDirection)name._direction
					};

					if ((name._direction == Direction.InputOutput || name._direction == Direction.Output) || pinfo.Value != DBNull.Value)
					{
						result.Add(sqlParam);
					}
				}
				else
				{
					SqlParameter sqlParam = new SqlParameter(pinfo.Name, (name._dbType != null ? (SqlDbType)name._dbType : TypeConvertor.ToSqlDbType(p.Property.PropertyType)))
					{
						Value = pinfo.Value,
						Direction = (ParameterDirection)name._direction
					};
					if (name._size != 0)
					{
						sqlParam.Size = name._size;
					}
					if (name._precision > 0)
					{
						sqlParam.Precision = name._precision;
					}

					if (name._scale > 0)
					{
						sqlParam.Scale = name._scale;
					}
					if ((name._direction == Direction.InputOutput || name._direction == Direction.Output) || pinfo.Value != DBNull.Value)
					{
						result.Add(sqlParam);
					}
				}
			});

			if (additionalParams != null && additionalParams.Length > 0)
			{
				result.AddRange(additionalParams);
			}

			return result;

		}
		public void ToObject(object obj, List<SqlParameter> sqlParameters = null)
		{
			if (sqlParameters.Where(s => s.Direction == ParameterDirection.Output || s.Direction == ParameterDirection.InputOutput).Any())
			{
				var props = (
				from p in obj.GetType().GetProperties()
				let nameAttr = p.GetCustomAttributes(typeof(QueryParamAttribute), true)
				let ignoreAttr = p.GetCustomAttributes(typeof(QueryParamIgnoreAttribute), true)
				select new { Property = p, Names = nameAttr, Ignores = ignoreAttr }).ToList();

				List<KeyValuePair<string, string>> flds = new List<KeyValuePair<string, string>>();
				props.ForEach(p =>
				{
					if (p.Ignores != null && p.Ignores.Length > 0)
					{
						return;
					}

					QueryParamAttribute name = p.Names.FirstOrDefault() as QueryParamAttribute;
					if (name == null)
					{
						return;
					}

					QueryParamInfo pinfo = new QueryParamInfo();

					if (name != null && !string.IsNullOrWhiteSpace(name._name))
					{
						pinfo.Name = name._name.Replace("@", "");
					}
					else
					{
						pinfo.Name = p.Property.Name.Replace("@", "");
					}

					flds.Add(new KeyValuePair<string, string>(p.Property.Name.Replace("@", ""), pinfo.Name));
				});

				foreach (SqlParameter item in sqlParameters.Where(s => s.Direction == ParameterDirection.Output || s.Direction == ParameterDirection.InputOutput))
				{

					PropertyInfo prop = obj.GetType().GetProperty(flds.Where(f => f.Value == item.ParameterName).FirstOrDefault().Key, BindingFlags.Public | BindingFlags.Instance);
					if (prop != null && prop.CanWrite)
					{
						if (item.Value == DBNull.Value)
						{
							prop.SetValue(obj, null);
						}
						else
						{
							prop.SetValue(obj, item.Value);
						}
					}
				}
			}

		}
	}

	/// <summary>
	/// Convert a base data type to another base data type
	/// </summary>
	public sealed class TypeConvertor
	{

		private struct DbTypeMapEntry
		{
			public Type Type;
			public DbType DbType;
			public SqlDbType SqlDbType;
			public DbTypeMapEntry(Type type, DbType dbType, SqlDbType sqlDbType)
			{
				Type = type;
				DbType = dbType;
				SqlDbType = sqlDbType;
			}

		};

		private static readonly ArrayList _DbTypeList = new ArrayList();

		#region Constructors

		static TypeConvertor()
		{
			DbTypeMapEntry dbTypeMapEntry
			= new DbTypeMapEntry(typeof(bool), DbType.Boolean, SqlDbType.Bit);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(byte), DbType.Double, SqlDbType.TinyInt);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(byte[]), DbType.Binary, SqlDbType.Image);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(DateTime), DbType.DateTime, SqlDbType.DateTime);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(decimal), DbType.Decimal, SqlDbType.Decimal);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(double), DbType.Double, SqlDbType.Float);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(Guid), DbType.Guid, SqlDbType.UniqueIdentifier);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(short), DbType.Int16, SqlDbType.SmallInt);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(int), DbType.Int32, SqlDbType.Int);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(long), DbType.Int64, SqlDbType.BigInt);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(object), DbType.Object, SqlDbType.Variant);
			_DbTypeList.Add(dbTypeMapEntry);

			dbTypeMapEntry
			= new DbTypeMapEntry(typeof(string), DbType.String, SqlDbType.VarChar);
			_DbTypeList.Add(dbTypeMapEntry);

		}

		private TypeConvertor()
		{

		}

		#endregion

		#region Methods

		/// <summary>
		/// Convert db type to .Net data type
		/// </summary>
		/// <param name="dbType"></param>
		/// <returns></returns>
		public static Type ToNetType(DbType dbType)
		{
			DbTypeMapEntry entry = Find(dbType);
			return entry.Type;
		}

		/// <summary>
		/// Convert TSQL type to .Net data type
		/// </summary>
		/// <param name="sqlDbType"></param>
		/// <returns></returns>
		public static Type ToNetType(SqlDbType sqlDbType)
		{
			DbTypeMapEntry entry = Find(sqlDbType);
			return entry.Type;
		}

		/// <summary>
		/// Convert .Net type to Db type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static DbType ToDbType(Type type)
		{
			DbTypeMapEntry entry = Find(type);
			return entry.DbType;
		}

		/// <summary>
		/// Convert TSQL data type to DbType
		/// </summary>
		/// <param name="sqlDbType"></param>
		/// <returns></returns>
		public static DbType ToDbType(SqlDbType sqlDbType)
		{
			DbTypeMapEntry entry = Find(sqlDbType);
			return entry.DbType;
		}

		/// <summary>
		/// Convert .Net type to TSQL data type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static SqlDbType ToSqlDbType(Type type)
		{
			DbTypeMapEntry entry = Find(type);
			return entry.SqlDbType;
		}

		/// <summary>
		/// Convert DbType type to TSQL data type
		/// </summary>
		/// <param name="dbType"></param>
		/// <returns></returns>
		public static SqlDbType ToSqlDbType(DbType dbType)
		{
			DbTypeMapEntry entry = Find(dbType);
			return entry.SqlDbType;
		}

		private static DbTypeMapEntry Find(Type type)
		{
			object retObj = null;
			for (int i = 0; i < _DbTypeList.Count; i++)
			{
				DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
				if (entry.Type == (Nullable.GetUnderlyingType(type) ?? type))
				{
					retObj = entry;
					break;
				}
			}
			if (retObj == null)
			{
				throw
				new ApplicationException("Referenced an unsupported Type " + type.ToString());
			}

			return (DbTypeMapEntry)retObj;
		}

		private static DbTypeMapEntry Find(DbType dbType)
		{
			object retObj = null;
			for (int i = 0; i < _DbTypeList.Count; i++)
			{
				DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
				if (entry.DbType == dbType)
				{
					retObj = entry;
					break;
				}
			}
			if (retObj == null)
			{
				throw
				new ApplicationException("Referenced an unsupported DbType " + dbType.ToString());
			}

			return (DbTypeMapEntry)retObj;
		}

		private static DbTypeMapEntry Find(SqlDbType sqlDbType)
		{
			object retObj = null;
			for (int i = 0; i < _DbTypeList.Count; i++)
			{
				DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
				if (entry.SqlDbType == sqlDbType)
				{
					retObj = entry;
					break;
				}
			}
			if (retObj == null)
			{
				throw
				new ApplicationException("Referenced an unsupported SqlDbType");
			}

			return (DbTypeMapEntry)retObj;
		}

		#endregion
	}
}
