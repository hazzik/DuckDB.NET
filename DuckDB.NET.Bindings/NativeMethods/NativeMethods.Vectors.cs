﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DuckDB.NET.Native;

public partial class NativeMethods
{
	public static class Vectors
	{
		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_vector_get_column_type")]
		public static extern DuckDBLogicalType DuckDBVectorGetColumnType(IntPtr vector);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_vector_get_data")]
		public static extern unsafe void* DuckDBVectorGetData(IntPtr vector);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_vector_get_validity")]
		public static extern unsafe ulong* DuckDBVectorGetValidity(IntPtr vector);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_list_vector_get_child")]
		public static extern IntPtr DuckDBListVectorGetChild(IntPtr vector);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_list_vector_get_size")]
		public static extern long DuckDBListVectorGetSize(IntPtr vector);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_struct_vector_get_child")]
		public static extern IntPtr DuckDBStructVectorGetChild(IntPtr vector, long index);

		[DllImport(DuckDbLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "duckdb_array_vector_get_child")]
		public static extern IntPtr DuckDBArrayVectorGetChild(IntPtr vector);
	}
}
