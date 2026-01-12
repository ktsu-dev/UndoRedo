// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Helper class for argument validation
/// </summary>
internal static class Guard
{
	/// <summary>
	/// Throws an ArgumentNullException if argument is null
	/// </summary>
	/// <param name="argument">The argument to check</param>
	/// <param name="paramName">The parameter name</param>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Parameter name captured via CallerArgumentExpression for exception message")]
	[SuppressMessage("Style", "IDE0022:Use expression body for method", Justification = "Conditional compilation makes expression body unsuitable")]
	public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
	{
#if NET6_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(argument, paramName);
#else
		if (argument is null)
		{
			throw new ArgumentNullException(paramName);
		}
#endif
	}
}
