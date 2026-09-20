/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AssemblyInfo.cs
수정일 : 2026-09-20

# 설명
Xeri UI Runtime 내부 계약을 같은 패키지의 Editor와 Test Assembly에 제한적으로 공개한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("inonego.Xeri.UI.Editor")]
[assembly: InternalsVisibleTo("inonego.Xeri.UI.TEST.EDIT")]
[assembly: InternalsVisibleTo("inonego.Xeri.UI.TEST.PLAY")]
[assembly: InternalsVisibleTo("inonego.Xeri.UI.Editor.TEST.EDIT")]
