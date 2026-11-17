module CharProbe {

  const LETTERS: set<char> := { c: char | 'a' <= c <= 'z' || 'A' <= c <= 'Z' }

  function LowerA(c: char): char
    ensures Result in LETTERS || Result == c
  {
    if 'A' <= c <= 'Z' then LowerPair[c] else c
  }

  map LowerPair: char -> char
  // 公理化：每个大写字母映射到某个小写字母，且二次映射不变
  axiom PairDefined(c: char)
    requires 'A' <= c <= 'Z'
    ensures c in LowerPair.Domain() && LowerPair[c] in LETTERS && !('A' <= LowerPair[c] <= 'Z')

  lemma ProbeIdem(c: char)
    ensures LowerA(LowerA(c)) == LowerA(c)
  {
  }
}
