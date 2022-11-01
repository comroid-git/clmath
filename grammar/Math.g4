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

PAR_L: '(';
PAR_R: ')';
IDX_L: '[';
IDX_R: ']';
ACC_L: '{';
ACC_R: '}';

DOT: '.' | ',';
SEMICOLON: ';';
DOLLAR: '$';
EQUALS: '=';
DIGIT: [0-9];
num: OP_SUB? DIGIT+ (DOT DIGIT+)?;
CHAR: [a-zA-Z_];
word: CHAR+;
evalVar: name=word EQUALS expr;
eval: DOLLAR name=word (ACC_L evalVar (SEMICOLON evalVar)* ACC_R)?;

WS: [ \n\r\t] -> channel(HIDDEN);

idxExpr: IDX_L n=expr IDX_R;

op_1
    : OP_MUL
    | OP_DIV
    | OP_MOD
    | POW
;
op_2
    : OP_ADD
    | OP_SUB
;

// functions
frac: FRAC PAR_L x=expr PAR_R PAR_L y=expr PAR_R;
fx: func PAR_L x=expr PAR_R;
root: ROOT i=idxExpr? PAR_L x=expr PAR_R;

// expressions
expr
    : l=expr op_1 r=expr    #exprOp1
    | PAR_L n=expr PAR_R    #exprPar
    | frac                  #exprFrac
    | fx                    #exprFunc
    | x=expr FACTORIAL      #exprFact
    | root                  #exprRoot
    | num                   #exprNum
    | word                  #exprId
    | eval                  #exprEval
    | l=expr op_2 r=expr    #exprOp2
;

UNMATCHED: . ; // raise errors on unmatched
