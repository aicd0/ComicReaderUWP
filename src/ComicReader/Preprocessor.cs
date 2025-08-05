// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader;

/// <summary>
/// Use to ensure all the preprocessor macros are properly defined.
/// </summary>
internal class Preprocessor
{
#if x86 || x64 || ARM64
#else
#error Architecture not defined.
#endif
}
