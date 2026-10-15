import fs from 'node:fs';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';
import { orderingSourceFiles, relativeToRoot } from './ordering-scope';

/**
 * U-X4 / C-6 / ADR-FE-092 — **no arithmetic on money in the browser**. HO-ORD-002 §12.3 rule 2, as an
 * AST scan (so a comment or a string never trips it): in the Ordering files there is no
 * `Number(` / `parseFloat(` / `parseInt(` of a money-named value, no `+ - * / %` (or compound
 * assignment, or unary `+`/`-`) with a money-named operand. Money is a server string that
 * `formatMoney` renders; totals, change due and refund suggestions come from the server.
 *
 * "Money-named" = the operand's own name (identifier or property name) contains amount, total, price,
 * charge, discount, tax, refund or changeDue. A string concatenation (`+` with a string or template
 * operand) is not arithmetic. `lib/format/money.ts` is the one allowed place and is not scanned.
 */
const MONEY_NAME = /(amount|total|price|charge|discount|tax|refund|changedue)/i;
const NOT_MONEY = /^(totalCount|totalPages|taxRatePercent|pageTotal)$/;
const ARITHMETIC = new Set<ts.SyntaxKind>([
  ts.SyntaxKind.PlusToken,
  ts.SyntaxKind.MinusToken,
  ts.SyntaxKind.AsteriskToken,
  ts.SyntaxKind.SlashToken,
  ts.SyntaxKind.PercentToken,
  ts.SyntaxKind.PlusEqualsToken,
  ts.SyntaxKind.MinusEqualsToken,
  ts.SyntaxKind.AsteriskEqualsToken,
  ts.SyntaxKind.SlashEqualsToken,
]);

function leafName(node: ts.Node): string | null {
  if (ts.isParenthesizedExpression(node) || ts.isNonNullExpression(node) || ts.isAsExpression(node)) {
    return leafName(node.expression);
  }
  if (ts.isIdentifier(node)) return node.text;
  if (ts.isPropertyAccessExpression(node)) return node.name.text;
  return null;
}

function isMoneyOperand(node: ts.Node): boolean {
  const name = leafName(node);
  return name !== null && MONEY_NAME.test(name) && !NOT_MONEY.test(name);
}

function isStringish(node: ts.Node): boolean {
  return (
    ts.isStringLiteral(node) ||
    ts.isNoSubstitutionTemplateLiteral(node) ||
    ts.isTemplateExpression(node)
  );
}

export function findMoneyArithmetic(fileName: string, source: string): string[] {
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true);
  const offences: string[] = [];
  const report = (node: ts.Node, why: string) => {
    const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart());
    offences.push(`${fileName}:${line + 1} ${why}`);
  };

  const visit = (node: ts.Node) => {
    if (ts.isBinaryExpression(node) && ARITHMETIC.has(node.operatorToken.kind)) {
      const concatenation =
        node.operatorToken.kind === ts.SyntaxKind.PlusToken &&
        (isStringish(node.left) || isStringish(node.right));
      if (!concatenation && (isMoneyOperand(node.left) || isMoneyOperand(node.right))) {
        report(node, `arithmetic on a money value: ${node.getText(sourceFile)}`);
      }
    }
    if (
      ts.isPrefixUnaryExpression(node) &&
      (node.operator === ts.SyntaxKind.PlusToken || node.operator === ts.SyntaxKind.MinusToken) &&
      isMoneyOperand(node.operand)
    ) {
      report(node, `unary sign on a money value: ${node.getText(sourceFile)}`);
    }
    if (ts.isCallExpression(node)) {
      const callee = node.expression.getText(sourceFile);
      if (/^(Number|parseFloat|parseInt|Number\.parseFloat|Number\.parseInt)$/.test(callee)) {
        if (node.arguments.some((argument) => isMoneyOperand(argument))) {
          report(node, `conversion of a money value: ${node.getText(sourceFile)}`);
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(sourceFile);
  return offences;
}

describe('C-6 — no money arithmetic (U-X4)', () => {
  it('the Ordering files do arithmetic on no money value', () => {
    const offences = orderingSourceFiles().flatMap((file) =>
      findMoneyArithmetic(relativeToRoot(file), fs.readFileSync(file, 'utf8')),
    );
    expect(offences).toEqual([]);
  });

  it('the scan does catch arithmetic (a guard that cannot fail proves nothing)', () => {
    expect(findMoneyArithmetic('x.ts', 'const t = line.amount * 2;')).toHaveLength(1);
    expect(findMoneyArithmetic('x.ts', 'const t = Number(order.grandTotal.amount);')).toHaveLength(1);
    expect(findMoneyArithmetic('x.ts', 'const t = a.price + b.price;')).toHaveLength(1);
    expect(findMoneyArithmetic('x.ts', 'total += tax;')).toHaveLength(1);
    expect(findMoneyArithmetic('x.ts', 'const label = "BDT " + amount;')).toHaveLength(0);
    expect(findMoneyArithmetic('x.ts', 'const n = changes.length - 1;')).toHaveLength(0);
  });
});
