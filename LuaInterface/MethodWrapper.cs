using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using LuaInterface.Helpers;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Cached method</summary>
	struct MethodCache
	{
		private MethodBase _cachedMethod;

		public MethodBase cachedMethod
		{
			get
			{
				return _cachedMethod;
			}
			set
			{
				_cachedMethod = value;
				MethodInfo mi = value as MethodInfo;
				if (mi != null)
				{
					//SJD this is guaranteed to be correct irrespective of actual name used for type..
					IsReturnVoid = mi.ReturnType == typeof(void);
				}
			}
		}

		public bool IsReturnVoid;

		// List or arguments
		public object[] args;
		// Positions of out parameters
		public int[] outList;
		// Types of parameters
		public MethodArgs[] argTypes;
	}

	/// <summary>Parameter information</summary>
	struct MethodArgs
	{
		// Position of parameter
		public int index;
		// Type-conversion function
		public ExtractValue extractValue;

		public bool isParamsArray;

		public Type paramsArrayType;
	}

	/// <summary>[-0, +0, m] Argument extraction with type-conversion function</summary>
	delegate object ExtractValue(lua.State L, int index);

	/// <summary>[-0, +0, -] Argument checking function, returns true if the type can be extracted</summary>
	delegate bool CheckValue(lua.State L, int index);

	/// <summary>Wrapper class for methods/constructors accessed from Lua.</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	class LuaMethodWrapper
	{
		private readonly ObjectTranslator _Translator;
		private readonly MethodBase _Method;
		private readonly string _MethodName;
		private readonly MemberInfo[] _Members;
		private readonly ExtractValue _ExtractTarget;
		private readonly object _Target;
		private readonly BindingFlags _BindingType;

		private MethodCache _LastCalledMethod = new MethodCache();

		/// <summary>Constructs the wrapper for a known MethodBase instance</summary>
		public LuaMethodWrapper(ObjectTranslator translator, object target, IReflect targetType, MethodBase method)
		{
			_Translator = translator;
			_Target = target;
			if (targetType != null)
				_ExtractTarget = translator.typeChecker.getExtractor(targetType);
			_Method = method;
			_MethodName = method.Name;

			_BindingType = method.IsStatic ? BindingFlags.Static : BindingFlags.Instance;
		}
		/// <summary>Constructs the wrapper for a known method name</summary><exception cref="NullReferenceException">All arguments are required</exception>
		public LuaMethodWrapper(ObjectTranslator translator, IReflect targetType, string methodName, BindingFlags bindingType)
		{
			Debug.Assert(translator != null && targetType != null && methodName != null);
			_Translator = translator;
			_MethodName = methodName;

			_ExtractTarget = translator.typeChecker.getExtractor(targetType);

			_BindingType = bindingType;

			_Members = targetType.UnderlyingSystemType.GetMember(methodName, MemberTypes.Method, bindingType | luanet.LuaBindingFlags);
		}


		/// <summary>Calls the method. Receives the arguments from the Lua stack and returns values in it.</summary>
		public int call(lua.State L)
		{
			using (luanet.entercfunction(L, _Translator.interpreter))
			{
				MethodBase methodToCall = _Method;
				object targetObject = _Target;
				bool failedCall = true;
				int nReturnValues = 0;

				luaL.checkstack(L, 5, "MethodWrapper.call");

				bool isStatic = (_BindingType & BindingFlags.Static) == BindingFlags.Static;

				if (methodToCall == null) // Method from name
				{
					targetObject = isStatic ? null : _ExtractTarget(L, 1);

					//lua.remove(L,1); // Pops the receiver
					if (_LastCalledMethod.cachedMethod != null) // Cached?
					{
						int numStackToSkip = isStatic ? 0 : 1; // If this is an instance invoe we will have an extra arg on the stack for the targetObject
						int numArgsPassed = lua.gettop(L) - numStackToSkip;
						MethodBase method = _LastCalledMethod.cachedMethod;

						if (numArgsPassed == _LastCalledMethod.argTypes.Length) // No. of args match?
						{
							luaL.checkstack(L, _LastCalledMethod.outList.Length + 6, "MethodWrapper.call");
							object[] args = _LastCalledMethod.args;

							try
							{
								for (int i = 0; i < _LastCalledMethod.argTypes.Length; i++)
								{
									MethodArgs type = _LastCalledMethod.argTypes[i];
									object luaParamValue = type.extractValue(L, i + 1 + numStackToSkip);

									args[type.index] = _LastCalledMethod.argTypes[i].isParamsArray
										? ObjectTranslator.TableToArray(luaParamValue,type.paramsArrayType)
										: luaParamValue;

									if (args[type.index] == null && !lua.isnil(L, i + 1 + numStackToSkip))
										throw new LuaException("argument number " + (i + 1) + " is invalid");
								}
								_Translator.pushReturnValue(L, method.IsConstructor
									? ((ConstructorInfo) method).Invoke(args)
									: method.Invoke(targetObject, args)  );

								failedCall = false;
							}
							catch (TargetInvocationException ex) { return _Translator.throwError(L, luaclr.verifyex(ex.InnerException)); }
							catch (Exception ex)
							{
								if (_Members.Length == 1) // Is the method overloaded?
									return luaL.error(L, "method call failed ({0})", ex.Message); // No, throw error
							}
						}
					}

					// Cache miss
					if (failedCall)
					{
						// System.Diagnostics.Debug.WriteLine("cache miss on " + methodName);

						// If we are running an instance variable, we can now pop the targetObject from the stack
						if (!isStatic)
						{
							if (targetObject == null)
								return luaL.error(L, String.Format("instance method '{0}' requires a non null target object", _MethodName));

							lua.remove(L, 1); // Pops the receiver
						}

						bool hasMatch = false;
						string candidateName = null;

						foreach (MemberInfo member in _Members)
						{
							candidateName = member.ReflectedType.Name + "." + member.Name;

							MethodBase m = (MethodInfo)member;

							bool isMethod = _Translator.matchParameters(L, m, ref _LastCalledMethod);
							if (isMethod)
							{
								hasMatch = true;
								break;
							}
						}
						if (!hasMatch)
						{
							return luaL.error(L, (candidateName == null)
								? "invalid arguments to method call"
								: "invalid arguments to method: " + candidateName  );
						}
					}
				}
				else // Method from MethodBase instance
				{
					if (methodToCall.ContainsGenericParameters)
					{
						// bool isMethod = //* not used
						_Translator.matchParameters(L, methodToCall, ref _LastCalledMethod);

						if (methodToCall.IsGenericMethodDefinition)
						{
							//need to make a concrete type of the generic method definition
							var args = _LastCalledMethod.args;
							var typeArgs = new Type[args.Length];

							for (int i = 0; i < args.Length; ++i)
								typeArgs[i] = args[i].GetType();

							MethodInfo concreteMethod = ((MethodInfo) methodToCall).MakeGenericMethod(typeArgs);

							_Translator.pushReturnValue(L, concreteMethod.Invoke(targetObject, args));

							failedCall = false;
						}
						else if (methodToCall.ContainsGenericParameters)
							return luaL.error(L, "unable to invoke method on generic class as the current method is an open generic method");
					}
					else
					{
						if (!methodToCall.IsStatic && !methodToCall.IsConstructor && targetObject == null)
						{
							targetObject = _ExtractTarget(L, 1);
							lua.remove(L, 1); // Pops the receiver
						}

						if (!_Translator.matchParameters(L, methodToCall, ref _LastCalledMethod))
							return luaL.error(L, "invalid arguments to method call");
					}
				}

				if (failedCall)
				{
					luaL.checkstack(L, _LastCalledMethod.outList.Length + 6, "MethodWrapper.call");
					try
					{
						_Translator.pushReturnValue(L, _LastCalledMethod.cachedMethod.IsConstructor
							? ((ConstructorInfo) _LastCalledMethod.cachedMethod).Invoke(_LastCalledMethod.args)
							: _LastCalledMethod.cachedMethod.Invoke(isStatic ? null : targetObject, _LastCalledMethod.args)  );
					}
					catch (TargetInvocationException ex) { return _Translator.throwError(L, luaclr.verifyex(ex.InnerException)); }
					catch (Exception ex) { return luaL.error(L, "method call failed ({0})", ex.Message); }
				}

				// Pushes out and ref return values
				foreach (int arg in _LastCalledMethod.outList)
				{
					nReturnValues++;
					_Translator.pushReturnValue(L, _LastCalledMethod.args[arg]);
				}

				//by isSingle 2010-09-10 11:26:31
				//Desc:
				//  if not return void,we need add 1,
				//  or we will lost the function's return value
				//  when call dotnet function like "int foo(arg1,out arg2,out arg3)" in lua code
				if (!_LastCalledMethod.IsReturnVoid && nReturnValues > 0)
				{
					nReturnValues++;
				}

				return nReturnValues < 1 ? 1 : nReturnValues;
			}
		}
	}




	/// <summary>
	/// We keep track of what delegates we have auto attached to an event - to allow us to cleanly exit a LuaInterface session
	/// </summary>
	class EventHandlerContainer : IDisposable
	{
		Dictionary<Delegate, RegisterEventHandler> dict = new Dictionary<Delegate, RegisterEventHandler>();

		public void Add(Delegate handler, RegisterEventHandler eventInfo)
		{
			dict.Add(handler, eventInfo);
		}

		public void Remove(Delegate handler)
		{
			bool found = dict.Remove(handler);
			Debug.Assert(found);
		}

		/// <summary>
		/// Remove any still registered handlers
		/// </summary>
		public void Dispose()
		{
			foreach (KeyValuePair<Delegate, RegisterEventHandler> pair in dict)
			{
				pair.Value.RemovePending(pair.Key);
			}

			dict.Clear();
		}
	}


	/// <summary>Wrapper class for events that does registration/deregistration of event handlers.</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	class RegisterEventHandler
	{
		object target;
		EventInfo eventInfo;
		EventHandlerContainer pendingEvents;

		public RegisterEventHandler(EventHandlerContainer pendingEvents, object target, EventInfo eventInfo)
		{
			this.target = target;
			this.eventInfo = eventInfo;
			this.pendingEvents = pendingEvents;
		}


		/// <summary>Adds a new event handler</summary>
		public Delegate Add(LuaFunction function)
		{
#if __NOGEN__
			//throw new NotSupportedException(L,"Delegates not implemented");
			return null;
#else
			//CP: Fix by Ben Bryant for event handling with one parameter
			//link: http://luaforge.net/forum/message.php?msg_id=9266
			Delegate handlerDelegate = CodeGeneration.Instance.GetDelegate(eventInfo.EventHandlerType, function);
			eventInfo.AddEventHandler(target, handlerDelegate);
			pendingEvents.Add(handlerDelegate, this);

			return handlerDelegate;
#endif
		}

		/// <summary>Removes an existing event handler</summary>
		public void Remove(Delegate handlerDelegate)
		{
			RemovePending(handlerDelegate);
			pendingEvents.Remove(handlerDelegate);
		}

		/// <summary>Removes an existing event handler (without updating the pending handlers list)</summary>
		internal void RemovePending(Delegate handlerDelegate)
		{
			eventInfo.RemoveEventHandler(target, handlerDelegate);
		}
	}

	/// <summary>
	/// Base wrapper class for Lua function event handlers.
	/// Subclasses that do actual event handling are created at runtime.
	/// </summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	public class LuaEventHandler
	{
		public LuaFunction handler = null;

		// CP: Fix provided by Ben Bryant for delegates with one param
		// link: http://luaforge.net/forum/message.php?msg_id=9318
		public void handleEvent(object[] args)
		{
			handler.Call(args);
		}
		//public void handleEvent(object sender,object data)
		//{
		//    handler.call(new object[] { sender,data },new Type[0]);
		//}
	}

	/// <summary>
	/// Wrapper class for Lua functions as delegates.
	/// Subclasses with correct signatures are created at runtime.
	/// </summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	public class LuaDelegate
	{
		public Type[] returnTypes;
		public LuaFunction function;
		public LuaDelegate()
		{
			function = null;
			returnTypes = null;
		}
		public object callFunction(object[] args, object[] inArgs, int[] outArgs)
		{
			// args is the return array of arguments, inArgs is the actual array
			// of arguments passed to the function (with in parameters only), outArgs
			// has the positions of out parameters
			object returnValue;
			int iRefArgs;
			object[] returnValues = function.call(inArgs, returnTypes);
			if (returnTypes[0] == typeof(void))
			{
				returnValue = null;
				iRefArgs = 0;
			}
			else
			{
				returnValue = returnValues[0];
				iRefArgs = 1;
			}
			// Sets the value of out and ref parameters (from
			// the values returned by the Lua function).
			for (int i = 0; i < outArgs.Length; i++)
			{
				args[outArgs[i]] = returnValues[iRefArgs];
				iRefArgs++;
			}
			return returnValue;
		}
	}

	/// <summary>Static helper methods for Lua tables acting as CLR objects.</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	public class LuaClassHelper
	{
		/// <summary> Gets the function called name from the provided table, returning null if it does not exist</summary>
		public static LuaFunction getTableFunction(LuaTable luaTable, string name)
		{
			object o = luaTable.RawGet(name);
			var funcObj = o as LuaFunction;
			if (funcObj != null) return funcObj;
			o.TryDispose();
			return null;
		}
		/// <summary>Calls the provided function with the provided parameters</summary>
		public static object callFunction(LuaFunction function, object[] args, Type[] returnTypes, object[] inArgs, int[] outArgs)
		{
			// args is the return array of arguments, inArgs is the actual array
			// of arguments passed to the function (with in parameters only), outArgs
			// has the positions of out parameters
			object returnValue;
			int iRefArgs;
			object[] returnValues = function.call(inArgs, returnTypes);
			if (returnTypes[0] == typeof(void))
			{
				returnValue = null;
				iRefArgs = 0;
			}
			else
			{
				returnValue = returnValues[0];
				iRefArgs = 1;
			}
			for (int i = 0; i < outArgs.Length; i++)
			{
				args[outArgs[i]] = returnValues[iRefArgs];
				iRefArgs++;
			}
			return returnValue;
		}
	}
}