using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.UI;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;
using Newtonsoft.Json;

using System.Diagnostics;
namespace RedisEasyClient
{
	public static class Connector
	{
		//This will used for manage all request
		private static ConnectionMultiplexer Redis { get; set; }
		//Endpoint for Redis service
		private static string Domain { get; set; }
		//Default expires value for all items to be Stored on Redis
		private static TimeSpan DefaultExpires { get; set; }
		static Connector()
		{
			//Your web.config/app.config AppSettings key
			Domain = WebConfigurationManager.AppSettings["RedisHost"];

			//Your web.config/app.config AppSettings key
			var expires = WebConfigurationManager.AppSettings["RedisDefaultExpires"];
			if (expires == null)
				DefaultExpires = new TimeSpan(365, 0, 0, 0);
			else
			{
				var arExpires = expires.Split(':');
				int days = Convert.ToInt32(arExpires[0]),
					hours = Convert.ToInt32(arExpires[1]),
					minutes = Convert.ToInt32(arExpires[2]),
					seconds = Convert.ToInt32(arExpires[3]);
				DefaultExpires = new TimeSpan(days, hours, minutes, seconds);
			}
			Redis = ConnectionMultiplexer.Connect(Domain);
		}

		/// <summary>
		/// Store any object on Redis, the key will be a Id porterty of object.
		/// To another key for object (Ex.: FkClient), use "StoreCustomKeyOnCache" method.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="value">Object to be stored.</param>
		/// <returns>Return false when in case of any error.</returns>
		public static void StoreTypedOnCache<T>(T value)
		{
			var tipo = typeof(T).Name.ToLower();
			var key = value.GetId();
			var db = Redis.GetDatabase();
			db.HashSet(tipo, key.ToString(), JsonConvert.SerializeObject(value));
		}
		/// <summary>
		/// Story any object on Redis and sets up a Custom key retrieve it after.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="obj">Object to be stored.</param>
		/// <param name="key">Key to retrieve object after.</param>
		/// /// /// <param name="expiresIn">TTL. Default is 365 days.</param>
		/// <returns>Return false when in case of any error.</returns>
		public static void StoreCustomKeyOnCache<T>(T obj, string keyName, string keyValue)
		{
			var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			db.HashSet(tipo, keyValue, JsonConvert.SerializeObject(obj));
		}
		/// <summary>
		/// Get any object from Redis by a single Id.
		/// </summary>
		/// <typeparam name="T">Type of object to be retrieved.</typeparam>
		/// <param name="id">Id proterty value.</param>
		/// <returns>Id matched object. Null if any matchs.</returns>
		public static T GetTypedFromCache<T>(object id)
		{
			var tipo = typeof(T).Name.ToLower();
			var db = Redis.GetDatabase();
			var strObj = db.HashGet(tipo, id.ToString());
			if (string.IsNullOrEmpty(strObj))
				return default(T);
			return JsonConvert.DeserializeObject<T>(strObj);
		}
		/// <summary>
		/// Get any object from Redis by a custom key. (Stored by "StoreCustomKeyOnCache" method)
		/// </summary>
		/// <typeparam name="T">Type of object to be retrieved.</typeparam>
		/// <param name="key">Key property value.</param>
		/// <returns>Key matched object. Null if any matchs.</returns>
		public static T GetCustomKeyOnCache<T>(string keyName, string keyValue)
		{
			try
			{
				var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
				var db = Redis.GetDatabase();
				var strObj = db.HashGet(tipo, keyValue);
				if (string.IsNullOrEmpty(strObj))
					return default(T);	
				return JsonConvert.DeserializeObject<T>(strObj);
			}
			catch (Exception)
			{
				//return a null obj
				return JsonConvert.DeserializeObject<T>("{}");
			}
		}
		/// <summary>
		/// Drop any object from Redis by a custom key. (Stored by "StoreCustomKeyOnCache" method)
		/// </summary>
		/// <typeparam name="T">Type of object to be droped.</typeparam>
		/// <param name="key">Key property value.</param>
		/// <returns>Key matched object. False in case of errors.</returns>
		public static void DropCustomKeyFromCache<T>(string keyName, string keyValue)
		{
			var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			db.HashDelete(tipo, keyValue);
		}
		public static void DropTypedKeyFromCache<T>(string keyValue)
		{
			var tipo = typeof(T).Name.ToLower();
			var db = Redis.GetDatabase();
			db.HashDelete(tipo, keyValue);
		}
		/// <summary>
		/// Drop any object from Redis by object.
		/// To drop from another key for object (Ex.: FkClient), use "DropCustomKeyOnCache" method.
		/// </summary>
		/// <typeparam name="T">Type of object.</typeparam>
		/// <param name="value">Object to be droped.</param>
		/// <returns>Return false when in case of any error.</returns>
		public static void DropTypedOnCache<T>(T value)
		{
			var tipo = typeof(T).Name.ToLower();
			var key = value.GetId();
			var db = Redis.GetDatabase();
			db.HashDelete(tipo, key.ToString());
		}
		public static void DropByCustomType<T>(string keyName)
		{
			var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			var keys = db.HashKeys(tipo).ToList();
			foreach (var key in keys)
				db.HashDelete(tipo, key);
		}
		/// <summary>
		/// All keys will droped, except keys especified.
		/// </summary>
		/// <typeparam name="T">Type of object</typeparam>
		/// <param name="keyName">key to specific</param>
		/// <param name="exceptKeys">Excepts still store in cache</param>
		/// <returns>Qt of removed itens</returns>
		public static int DropExceptByCustomType<T>(string keyName, List<string> exceptKeys)
		{
			var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			List<string> keys = db.HashKeys(tipo).Select(i => i.ToString()).ToList();
			var toDelete = keys.Except(exceptKeys).ToList();
			foreach (var key in toDelete)
				db.HashDelete(tipo, key);
			return toDelete.Count();
		}

		public static int DropAllByStringHash(string hash)
		{
			var tipo = hash.ToLower();
			var db = Redis.GetDatabase();
			List<string> keys = db.HashKeys(tipo).Select(i => i.ToString()).ToList();
			foreach (var key in keys)
				db.HashDelete(tipo, key);
			return keys.Count;
		}
		public static int DropExceptByTyped<T>(List<string> exceptKeys)
		{
			var tipo = typeof(T).Name.ToLower();
			var db = Redis.GetDatabase();
			List<string> keys = db.HashKeys(tipo).Select(i => i.ToString()).ToList();
			var toDelete = keys.Except(exceptKeys).ToList();
			foreach (var key in toDelete)
				db.HashDelete(tipo, key);
			return toDelete.Count();
		}
		
		public static void DropSigleKeyOnCache<T>(string key)
		{
			var tipo = typeof(T).Name.ToLower();
			var db = Redis.GetDatabase();
			db.HashDelete(tipo, key);
		}
		public static void DropSigleKeyOnCache(string key)
		{
			var db = Redis.GetDatabase();
			db.KeyDelete(key);
		}
		public static void StoreSigleKeyOnCache(string key, object value, TimeSpan? expires = null)
		{
			var db = Redis.GetDatabase();
			db.StringSet(key, JsonConvert.SerializeObject(value), expires);
		}
		public static T GetSigleKeyFromCache<T>(string key)
		{
			var db = Redis.GetDatabase();
			var strObj = db.StringGet(key);
			if (string.IsNullOrEmpty(strObj))
				return default(T);
			return JsonConvert.DeserializeObject<T>(strObj);
		}
		public static List<T> GetAllItemsFromType<T>(string keyName = null)
		{
			var tipo = typeof(T).Name.ToLower();
			if (!string.IsNullOrEmpty(keyName))
				tipo += "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			var its = db.HashGetAll(tipo);
			var lst = its.Select(i => JsonConvert.DeserializeObject<T>(i.Value)).ToList();
			return lst;
		}
		public static List<string> GetAllKeysFromHash(string hash)
		{
			var db = Redis.GetDatabase();
			var its = db.HashKeys(hash).ToList();
			return its.Select(i => (string) i).ToList();
		}
		/// <summary>
		/// Get a count of inserted items of a object.
		/// </summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <returns>Qt of items of object.</returns>
		public static long GetQtItemsByType<T>()
		{
			var tipo = typeof(T).Name.ToLower();
			var db = Redis.GetDatabase();
			return db.HashLength(tipo);
		}
		public static long GetQtItemsByCustomTypeKey<T>(string keyName)
		{
			var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
			var db = Redis.GetDatabase();
			return db.HashLength(tipo);
		}
		private static object GetId<T>(this T obj)
		{
            var t = typeof(T);
			var props = t.GetProperties();
            var id = props.FirstOrDefault(i => i.Name.ToLower() == "id") ?? props.FirstOrDefault(i => i.Name.ToLower().Contains("id"));
			if (id != null)
				return id.GetValue(obj);
			return null;
		}

        public static List<T> GetAllList<T>(string keyName)
        {
            var tipo = typeof(T).Name.ToLower() + "_by_" + keyName.ToLower();
            var db = Redis.GetDatabase();
            var obj = db.HashGetAll(tipo).ToList();
            return obj.Select(i => JsonConvert.DeserializeObject<T>(i.Value)).ToList();
        }
        public static List<T> GetAllList<T>()
        {
            try
            {
                var tipo = typeof(T).Name.ToLower();
                var db = Redis.GetDatabase();
                var tamanho = GetQtItemsByType<T>();
                if (tamanho > 65000)
                {
                    var obj = db.HashKeys(tipo).ToList();
                    return obj.Select(i => GetTypedFromCache<T>(i)).ToList();
                }
                var obj2 = db.HashGetAll(tipo).ToList();
                return obj2.Select(i => JsonConvert.DeserializeObject<T>(i.Value)).ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }
        private static string Serialize(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
	}
}
