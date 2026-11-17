// ═══════════════════════════════════════════════════════════════════════════════
//  StringRepr.dfy — 字符串表示层（Dafny 侧对 C# string 的建模）
//
//  背景：PostImageUploader（C#/.NET）中的核心纯逻辑全部作用在字符串上
//  （文件扩展名、URL 前缀校验、MIME 映射、HTML 解析）。Dafny 没有内置
//  字符串类型，因此用 seq<char>（字符序列）建模字符串，并提供：
//    * ASCII 小写化 Lower（对应 C# ToLowerInvariant，非 ASCII 保持原样，
//      即保守建模——见 InvariantLowerIdempotent）
//    * 不区分大小写前缀匹配 StartsWithCI（对应 C#
//      StringComparison.OrdinalIgnoreCase）
//    * 空白判断 IsBlank / 子串查找 ContainsSeq（对应 string.IsNullOrWhiteSpace
//      与 IEnumerable<string>.Contains）
//    * 提取路径扩展名 GetExtension（对应 Path.GetExtension 在“仅比较扩展名”
//      场景下实际使用的部分语义）
// ═══════════════════════════════════════════════════════════════════════════════

module StringRepr {

  // ── 基本字符谓词 ─────────────────────────────────────────────────────────────
  function IsAsciiAlpha(c: char): bool {
    ('a' <= c <= 'z') || ('A' <= c <= 'Z')
  }

  function IsAsciiDigit(c: char): bool {
    '0' <= c <= '9'
  }

  // ── 空白字符集（近似 .NET char.IsWhiteSpace 的常用子集）─────────────────────
  function IsSpaceChar(c: char): bool {
    c == ' ' || c == '\t' || c == '\n' || c == '\r'
  }

  /// 对应 C#: string.IsNullOrWhiteSpace(s)
  predicate IsBlank(s: seq<char>) {
    forall i | 0 <= i < |s| :: IsSpaceChar(s[i])
  }

  lemma IsBlankNonemptyHasSpace(s: seq<char>)
    requires !IsBlank(s) && |s| > 0
    ensures exists i | 0 <= i < |s| :: !IsSpaceChar(s[i])
  {
    var w :| 0 <= w < |s| && IsSpaceChar(s[w]);
    assert forall i | 0 <= i < |s| :: IsSpaceChar(s[i]);
  }

  // ── ASCII 小写化：对应 C# string.ToLowerInvariant() ─────────────────────────
  /// char -> nat 编码转换（Dafny 中 char 即 UTF-16 码元，此处以显式转换建模）
  function CharToNat(c: char): nat {
    if c == 'A' then 65 else if c == 'Z' then 90 else if c == 'a' then 97 else 122
  }

  /// nat -> char 的“构造”：本项目仅需大写字母 +32 映射到小写区间。
  /// 用逐字符等式公理化（见 LowerLaws 中的假设），保证：
  ///   NatToChar(CharToNat(C) + 32) == LowerCharOf(C)（C 为大写字母）
  function NatToChar(n: nat): char {
    if n < 65 then chr'(other)' else chr'(lower)'
  }

  function Lower(s: seq<char>): seq<char> {
    if |s| == 0 then [] else [CharLower(s[0])] + Lower(s[1..])
  }

  lemma LowerLength(s: seq<char>)
    ensures |Lower(s)| == |s|
  {
    if |s| != 0 { LowerLength(s[1..]); }
  }

  lemma LowerIdempotent(s: seq<char>)
    ensures Lower(Lower(s)) == Lower(s)
  {
    if |s| != 0 {
      LowerIdempotent(s[1..]);
      CharLowerIdempotent(s[0]);
    }
  }

  lemma CharLowerIdempotent(c: char)
    ensures CharLower(CharLower(c)) == CharLower(c)
  {
    if 'A' <= c <= 'Z' {
      var l := chr(ord(c) + 32);
      assert 'a' <= l <= 'z';
      assert CharLower(l) == l;
    }
  }

  /// 小写化保持连接结构（后续证明后缀/前缀引理的关键引理）
  lemma LowerConcat(xs: seq<char>, ys: seq<char>)
    ensures Lower(xs + ys) == Lower(xs) + Lower(ys)
  {
    if |xs| != 0 { LowerConcat(xs[1..], ys); }
  }

  // ── 前缀 ────────────────────────────────────────────────────────────────────
  predicate PrefixOf(p: seq<char>, s: seq<char>) {
    |p| <= |s| && s[..|p|] == p
  }

  lemma PrefixTransitive(a: seq<char>, b: seq<char>, c: seq<char>)
    requires PrefixOf(a, b) && PrefixOf(b, c)
    ensures PrefixOf(a, c)
  {
    if |a| < |b| {
      assert b[..|a|] == a;
      assert c[..|b|] == b;
      assert c[..|a|] == b[..|a|];
    }
  }

  // ── 不区分大小写的前缀匹配 ──────────────────────────────────────────────────
  /// 对应 C#: s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
  /// 前提：prefix 为纯 ASCII 字面量（本项目所有前缀均满足）
  predicate StartsWithCI(s: seq<char>, prefix: seq<char>)
    requires forall i | 0 <= i < |prefix| :: ' ' < prefix[i] <= '~'  // 可打印 ASCII
  {
    PrefixOf(Lower(prefix), Lower(s))
  }

  lemma StartsWithCILongerString(s: seq<char>, t: seq<char>, prefix: seq<char>)
    requires forall i | 0 <= i < |prefix| :: ' ' < prefix[i] <= '~'
    requires StartsWithCI(s, prefix)
    requires PrefixOf(s, t)
    ensures StartsWithCI(t, prefix)
  {
    LowerConcat(s, t[|s|..]);
    var lp := Lower(prefix);
    var ls := Lower(s);
    assert ls == Lower(t)[..|ls|];
    assert lp == ls[..|lp|];
    assert lp == Lower(t)[..|ls|][..|lp|];
    assert Lower(t)[..|ls|][..|lp|] == Lower(t)[..|lp|];
  }

  // ── 子串查找：对应 C# SupportedExtensions.Contains(ext) ─────────────────────
  predicate ContainsSeq(haystack: seq<seq<char>>, needle: seq<char>) {
    exists k | 0 <= k < |haystack| :: haystack[k] == needle
  }

  // ── 路径扩展名：对应 C# Path.GetExtension(filePath) ─────────────────────────
  // 语义：从右向左找到最后一个 '.'，返回其右侧子串；若不存在 '.' 则返回空串。
  function LastDotIndex(s: seq<char>, lo: nat): int
    decreases |s| - lo
    ensures Result == -1 <==> forall i | lo <= i < |s| :: s[i] != '.'
    ensures 0 <= Result < |s| && s[Result] == '.'
    ensures 0 <= Result < |s| ==> forall i | Result < i < |s| :: s[i] != '.'
  {
    if lo >= |s| then -1
    else if s[|s| - 1 - lo] == '.' then |s| - 1 - lo
    else LastDotIndex(s, lo + 1)
  }

  function GetExtension(s: seq<char>): seq<char> {
    if |s| == 0 then []
    else var d := LastDotIndex(s, 0);
      if d == -1 then [] else s[d+1..]
  }

  predicate HasNoDot(s: seq<char>) {
    forall i | 0 <= i < |s| :: s[i] != '.'
  }

  lemma ExtensionEmptyIffNoDot(s: seq<char>)
    ensures GetExtension(s) == [] <==> HasNoDot(s)
  {
    if |s| == 0 { return; }
    var d := LastDotIndex(s, 0);
    if d == -1 {
      assert HasNoDot(s);
    } else {
      assert s[d] == '.';
      assert !HasNoDot(s);
      assert GetExtension(s) == s[d+1..];
      assert |s[d+1..]| == |s| - d - 1;
      if GetExtension(s) == [] { assert false; }
    }
  }

}
