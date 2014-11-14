using System;

namespace LuaInterface.LuaAPI
{
	using size_t = System.UIntPtr;
	public static unsafe class SizeTExtensions
	{
		/// <summary>Casts a <see cref="size_t"/> to an <see cref="Int32"/>, throwing <see cref="OverflowException"/> if it is too large.</summary>
		public static int ToInt32(this size_t value)
		{
			return checked((int)value.ToPointer());
		}

		/// <summary>Casts a <see cref="size_t"/> to an <see cref="Int32"/>, returning <see cref="Int32.MaxValue"/> if it is too large.</summary>
		public static int ToMaxInt32(this size_t value)
		{
			return value.ToPointer() > (void*)int.MaxValue
				? Int32.MaxValue
				: unchecked((int)value.ToPointer());
		}

		/// <summary>Casts an <see cref="Int32"/> to a <see cref="size_t"/>, throwing <see cref="OverflowException"/> if it is negative.</summary>
		public static size_t ToSizeType(this int value)
		{
			return new size_t(checked((void*)value));
		}
	}
}