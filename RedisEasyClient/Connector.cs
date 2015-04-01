using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using ServiceStack;
using ServiceStack.Redis;

namespace RedisEasyClient
{
	public class Connector
	{
		//This will used for manage all request
		public static IRedisClientsManager ClientsManager;
		//Endpoint for Redis service
		private static string Domain { get; set; }
		static Connector()
		{
			//Your web.config/app.config AppSettings key
			Domain = WebConfigurationManager.AppSettings["RedisHost"];
			ClientsManager = new PooledRedisClientManager(Domain);
		}
		/// <summary>
		/// Store any object on Redis, the key will be a Id porterty of object.
		/// To another key for object (Ex.: FkClient), use "StoreCustomKeyOnCache" method.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="value">Object to be stored.</param>
		/// <returns>Return false when in case of any error.</returns>
		public static bool StoreTypedInCache<T>(T value)
		{

			using (var client = ClientsManager.GetClient())
			{
				var id = value.GetId();
				var typed = client.As<T>();
				typed.DeleteById(id);
				typed.Store(value);
				return true;
			}
		}
		/// <summary>
		/// Story any object on Redis and sets up a Custom key retrieve it after.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="obj">Object to be stored.</param>
		/// <param name="key">Key to retrieve object after.</param>
		/// <returns>Return false when in case of any error.</returns>
		public static bool StoreCustomKeyOnCache<T>(T obj, string key)
		{
			using (var client = ClientsManager.GetClient())
			{
				var res = client.Get<T>(key);
				if (res != null)
					client.Delete(res);
				client.Set(key, obj);
				return true;
			}
		}
		/// <summary>
		/// Get any object from Redis by a single Id.
		/// </summary>
		/// <typeparam name="T">Type of object to be retrieved.</typeparam>
		/// <param name="id">Id proterty value.</param>
		/// <returns>Id matched object. Null if any matchs.</returns>
		public static T GetTypedFromCache<T>(object id)
		{
			using (var client = ClientsManager.GetClient())
			{
				var typed = client.As<T>();
				return typed.GetById(id);
			}
		}
		/// <summary>
		/// Get any object from Redis by a custom key. (Stored by "StoreCustomKeyOnCache" method)
		/// </summary>
		/// <typeparam name="T">Type of object to be retrieved.</typeparam>
		/// <param name="key">Key property value.</param>
		/// <returns>Key matched object. Null if any matchs.</returns>
		public static T GetCustomKeyOnCache<T>(string key)
		{
			using (var client = ClientsManager.GetClient())
			{
				var res = client.Get<T>(key);
				return res;
			}
		}
	}
}
