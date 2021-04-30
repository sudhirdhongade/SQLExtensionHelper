using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Project.Services
{
	public class CommonService : ICommonService
	{
		private readonly UnitOfWork _unitOfWork;

		public CommonService(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}

		public async Task<T> ExecuteScalarAsync<T>(object obj)
		{
			SqlParameterExtensions sqlParameterExtensions = new SqlParameterExtensions();
			List<SqlParameter> sqlParameters = obj != null ? sqlParameterExtensions.ToSqlParamsList(obj) : (List<SqlParameter>)obj;
			try
			{
				T result = default(T);
				result = await _unitOfWork.ExecuteScalarAsync<T>(sqlParameterExtensions.GetStoredProcedureName(obj), sqlParameters);
				sqlParameterExtensions.ToObject(obj, sqlParameters);
				return result;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<int> ExecuteNonQueryAsync(object obj)
		{
			SqlParameterExtensions sqlParameterExtensions = new SqlParameterExtensions();
			List<SqlParameter> sqlParameters = obj != null ? sqlParameterExtensions.ToSqlParamsList(obj) : (List<SqlParameter>)obj;
			try
			{
				int result = await _unitOfWork.ExecuteNonQueryAsync(sqlParameterExtensions.GetStoredProcedureName(obj), sqlParameters);
				sqlParameterExtensions.ToObject(obj, sqlParameters);
				return result;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<List<T>> ExecuteSPAsync<T>(object obj)
		{
			SqlParameterExtensions sqlParameterExtensions = new SqlParameterExtensions();
			List<SqlParameter> sqlParameters = obj != null ? sqlParameterExtensions.ToSqlParamsList(obj) : (List<SqlParameter>)obj;
			try
			{
				List<T> result = await _unitOfWork.ExecuteSPAsync<T>(sqlParameterExtensions.GetStoredProcedureName(obj), sqlParameters);
				sqlParameterExtensions.ToObject(obj, sqlParameters);
				return result;
			}
			catch (Exception)
			{
				throw;
			}
		}

		private string GetPK<T>()
		{
			Dictionary<string, string> _dict = new Dictionary<string, string>();

			PropertyInfo[] props = typeof(T).GetProperties();
			foreach (PropertyInfo prop in props)
			{
				IEnumerable<CustomAttributeData> propt = prop.CustomAttributes.Where(ele => ele.AttributeType == typeof(KeyAttribute));
				if (propt != null)
				{
					return prop.Name;
				}
			}
			return null;
		}

		public async Task<JsonOutputForList> FetchList<T>(SFParameters model, Expression<Func<T, object>>[] includes = null) where T : class
		{
			JsonOutputForList result = new JsonOutputForList();
			try
			{
				List<RequestById> resultList = await ExecuteSPAsync<RequestById>(model);
				List<long> IdList = resultList.Select(s => s.id).ToList();
				result.TotalCount = model.RowCount;
				result.PageNo = model.pageNo;
				result.RowsPerPage = model.rowsPerPage;

				if (resultList.Any())
				{
					string key = GetPK<T>();

					ParameterExpression item = Expression.Parameter(typeof(T), "item");
					Expression memberValue = key.Split('.').Aggregate((Expression)item, Expression.PropertyOrField);
					Type memberType = memberValue.Type;
					Expression exp = null;
					foreach (RequestById item1 in resultList)
					{
						BinaryExpression condition = Expression.Equal(memberValue, Expression.Constant(item1.id, memberType));
						exp = exp == null ? condition : Expression.OrElse(exp, condition);
					}

					IQueryable<T> clctn = _unitOfWork.Repository<T>().GetWhere(Expression.Lambda<Func<T, bool>>(exp, item), includes);

					List<T> ResultList = clctn.ToList();

					result.ResultList = ResultList.OrderBy(x => IdList.IndexOf(Convert.ToInt64(x.GetType().GetProperty(key).GetValue(x, null)))).ToList();
				}
				else
				{
					result.ResultList = new List<T>();
				}

				//IQueryable<T> clctn = _unitOfWork.Repository<T>().GetWhere(x => IdList.Contains(Convert.ToInt64(x.GetType().GetProperty(key).GetValue(x, null))));

				//IQueryable<T> clctn = _unitOfWork.Repository<T>().GetWhere(x => IdList.Contains(Convert.ToInt64(x.GetType().GetProperty(GetPK<T>()).GetValue(x, null))));
				//result.ResultList = clctn.ToList();
				//result.ResultList = clctn.OrderBy(x => IdList.IndexOf((Convert.ToInt64(x.GetType().GetProperty(GetPK(typeof(T))).GetValue(x, null))))).ToList();
				//result.ResultList = clctn.OrderBy(i => IdList.IndexOf(i.AppSettingId)).ToList();
			}
			catch (Exception ex)
			{
				throw ex;
			}
			return result;
		}
		//public async Task Save()
		//{
		//    try
		//    {
		//        await _unitOfWork.Save();
		//    }
		//    catch (Exception)
		//    {
		//        throw;
		//    }
		//}
		//public GenericRepository<T> ServiceRepository<T>() where T : class
		//{
		//    try
		//    {
		//        return _unitOfWork.Repository<T>();
		//    }
		//    catch (Exception)
		//    {
		//        throw;
		//    }
		//}

	}
}
