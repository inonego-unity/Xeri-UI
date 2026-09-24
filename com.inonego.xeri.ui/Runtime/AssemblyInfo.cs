/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AssemblyInfo.cs
수정일 : 2026-10-05

# 설명
Runtime assembly의 friend assembly 접근 권한을 선언한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("inonego.Xeri.UI.TEST.EDIT")]
[assembly: InternalsVisibleTo("inonego.Xeri.UI.TEST.PLAY")]
