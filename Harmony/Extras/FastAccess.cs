using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	/// <summary>Based on https://www.codeproject.com/Articles/14973/Dynamic-Code-Generation-vs-Reflection.</summary>
	/// <param name="source">Source for the.</param>
	/// <returns>An object.</returns>
	///
	public delegate object GetterHandler(object source);

	/// <summary>Handler, called when the setter.</summary>
	/// <param name="source">Source for the.</param>
	/// <param name="value"> The value.</param>
	///
	public delegate void SetterHandler(object source, object value);

	/// <summary>Handler, called when the instantiation.</summary>
	/// <returns>An object.</returns>
	///
	public delegate object InstantiationHandler();

	/// <summary>A fast access.</summary>
	public class FastAccess
	{
		/// <summary>Handler, called when the create instantiation.</summary>
		/// <exception cref="ApplicationException">Thrown when an Application error condition occurs.</exception>
		/// <param name="type">The type.</param>
		/// <returns>The new instantiation handler.</returns>
		///
		public static InstantiationHandler CreateInstantiationHandler(Type type)
		{
			var constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
			if (constructorInfo == null)
			{
				throw new ApplicationException(string.Format("The type {0} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).", type));
			}

			var dynamicMethod = new DynamicMethod("InstantiateObject_" + type.Name, MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(object), null, type, true);
			var generator = dynamicMethod.GetILGenerator();
			generator.Emit(OpCodes.Newobj, constructorInfo);
			generator.Emit(OpCodes.Ret);
			return (InstantiationHandler)dynamicMethod.CreateDelegate(typeof(InstantiationHandler));
		}

		/// <summary>Handler, called when the create getter.</summary>
		/// <param name="propertyInfo">Information describing the property.</param>
		/// <returns>The new getter handler.</returns>
		///
		public static GetterHandler CreateGetterHandler(PropertyInfo propertyInfo)
		{
			var getMethodInfo = propertyInfo.GetGetMethod(true);
			var dynamicGet = CreateGetDynamicMethod(propertyInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Call, getMethodInfo);
			BoxIfNeeded(getMethodInfo.ReturnType, getGenerator);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler)dynamicGet.CreateDelegate(typeof(GetterHandler));
		}

		/// <summary>Handler, called when the create getter.</summary>
		/// <param name="fieldInfo">Information describing the field.</param>
		/// <returns>The new getter handler.</returns>
		///
		public static GetterHandler CreateGetterHandler(FieldInfo fieldInfo)
		{
			var dynamicGet = CreateGetDynamicMethod(fieldInfo.DeclaringType);
			var getGenerator = dynamicGet.GetILGenerator();

			getGenerator.Emit(OpCodes.Ldarg_0);
			getGenerator.Emit(OpCodes.Ldfld, fieldInfo);
			BoxIfNeeded(fieldInfo.FieldType, getGenerator);
			getGenerator.Emit(OpCodes.Ret);

			return (GetterHandler)dynamicGet.CreateDelegate(typeof(GetterHandler));
		}

		/// <summary>Creates field getter.</summary>
		/// <param name="type"> The type.</param>
		/// <param name="names">A variable-length parameters list containing names.</param>
		/// <returns>The new field getter.</returns>
		///
		public static GetterHandler CreateFieldGetter(Type type, params string[] names)
		{
			foreach (var name in names)
			{
				if (AccessTools.Field(typeof(ILGenerator), name) != null)
					return CreateGetterHandler(AccessTools.Field(type, name));

				if (AccessTools.Property(typeof(ILGenerator), name) != null)
					return CreateGetterHandler(AccessTools.Property(type, name));
			}
			return null;
		}

		/// <summary>Handler, called when the create setter.</summary>
		/// <param name="propertyInfo">Information describing the property.</param>
		/// <returns>The new setter handler.</returns>
		///
		public static SetterHandler CreateSetterHandler(PropertyInfo propertyInfo)
		{
			var setMethodInfo = propertyInfo.GetSetMethod(true);
			var dynamicSet = CreateSetDynamicMethod(propertyInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			UnboxIfNeeded(setMethodInfo.GetParameters()[0].ParameterType, setGenerator);
			setGenerator.Emit(OpCodes.Call, setMethodInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler)dynamicSet.CreateDelegate(typeof(SetterHandler));
		}

		/// <summary>Handler, called when the create setter.</summary>
		/// <param name="fieldInfo">Information describing the field.</param>
		/// <returns>The new setter handler.</returns>
		///
		public static SetterHandler CreateSetterHandler(FieldInfo fieldInfo)
		{
			var dynamicSet = CreateSetDynamicMethod(fieldInfo.DeclaringType);
			var setGenerator = dynamicSet.GetILGenerator();

			setGenerator.Emit(OpCodes.Ldarg_0);
			setGenerator.Emit(OpCodes.Ldarg_1);
			UnboxIfNeeded(fieldInfo.FieldType, setGenerator);
			setGenerator.Emit(OpCodes.Stfld, fieldInfo);
			setGenerator.Emit(OpCodes.Ret);

			return (SetterHandler)dynamicSet.CreateDelegate(typeof(SetterHandler));
		}

		//

		static DynamicMethod CreateGetDynamicMethod(Type type)
		{
			return new DynamicMethod("DynamicGet_" + type.Name, typeof(object), new Type[] { typeof(object) }, type, true);
		}

		static DynamicMethod CreateSetDynamicMethod(Type type)
		{
			return new DynamicMethod("DynamicSet_" + type.Name, typeof(void), new Type[] { typeof(object), typeof(object) }, type, true);
		}

		static void BoxIfNeeded(Type type, ILGenerator generator)
		{
			if (type.IsValueType)
				generator.Emit(OpCodes.Box, type);
		}

		static void UnboxIfNeeded(Type type, ILGenerator generator)
		{
			if (type.IsValueType)
				generator.Emit(OpCodes.Unbox_Any, type);
		}
	}
}