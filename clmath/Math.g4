grammar Math;

OP_ADD: '+';
OP_SUB: '-';
OP_MUL: '*';
OP_DIV: '/';
OP_MOD: '%';

SIN: 'sin';
COS: 'cos';
TAN: 'tan';
LOG: 'log';
SEC: 'sec';
CSC: 'csc';
COT: 'cot';
HYP: 'hyp';
ARCSIN: 'a' 'rc'? SIN;
ARCCOS: 'a' 'rc'? COS;
ARCTAN: 'a' 'rc'? TAN;

func
    : SIN
    | COS
    | TAN
    | LOG
    | SEC
    | CSC
    | COT
    | HYP
    | ARCSIN
    | ARCCOS
    | ARCTAN
;

ROOT: 'sqrt' | 'root';
POW: '^';
FACTORIAL: '!';
FRAC: 'frac';
MEM: 'mem';
AS: 'as';

PAR_L: '(';
PAR_R: ')';
IDX_L: '[';
IDX_R: ']';
ACC_L: '{';
ACC_R: '}';

DOT: '.' | ',';
SEMICOLON: ';';
QUESTION: '?';
DOLLAR: '$';
EQUALS: '=';
ABS: '|';
DIGIT: [0-9];
num: OP_SUB? DIGIT+ (DOT DIGIT+)?;
CHAR: [a-zA-Z_µ];
word: CHAR (CHAR | DIGIT)*;
evalVar: name=word EQUALS expr;
eval: DOLLAR name=word (ACC_L evalVar (SEMICOLON evalVar)* ACC_R)?;

idxExpr: IDX_L n=expr IDX_R;

op_1
    : OP_MUL
    | OP_DIV
    | OP_MOD
;
op_2
    : OP_ADD
    | OP_SUB
;

// functions
frac: FRAC PAR_L x=expr PAR_R PAR_L y=expr PAR_R;
fx: func PAR_L x=expr PAR_R;
root: ROOT i=idxExpr? PAR_L x=expr PAR_R;
abs: ABS x=expr ABS;

// expressions
expr
    // parentheses
    : PAR_L n=expr PAR_R            #exprPar
    // numbers
    | num                           #exprNum
    // unit application
    | expr unit=word                #exprUnit
    // math OPs
    | x=expr POW y=expr             #exprPow
    | l=expr op_1 r=expr            #exprOp1
    | l=expr op_2 r=expr            #exprOp2
    | frac                          #exprFrac
    | fx                            #exprFunc
    | x=expr FACTORIAL              #exprFact
    | root                          #exprRoot
    // variables
    | MEM (IDX_L n=expr IDX_R)?     #exprMem
    | word                          #exprId
    // func evaluation
    | eval                          #exprEval
    // grammarly late handlers
    | abs                           #exprAbs
    // unit handling
    | expr AS unit=word             #exprUnitCast
    | expr QUESTION                 #exprUnitNormalize
;
equation: lhs=expr EQUALS rhs=expr SEMICOLON;
unitFile: .*? equation*;

WS: [ \r\n\t] -> channel(HIDDEN);
UNMATCHED: . ; // raise errors on unmatched
