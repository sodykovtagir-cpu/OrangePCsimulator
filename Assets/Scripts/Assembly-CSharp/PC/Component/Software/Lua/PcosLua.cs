using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PC.Component.Software.Lua
{
	public sealed class PcosLuaException : Exception
	{
		public int Line { get; }
		public PcosLuaException(string message, int line = 0) : base(line > 0 ? ("line " + line + ": " + message) : message)
		{
			Line = line;
		}
	}

	public enum LuaType
	{
		Nil,
		Bool,
		Number,
		String,
		Table,
		Function
	}

	public sealed class LuaTable
	{
		public readonly Dictionary<LuaValue, LuaValue> Map = new Dictionary<LuaValue, LuaValue>();

		public LuaValue Get(LuaValue key)
		{
			LuaValue v;
			return Map.TryGetValue(key, out v) ? v : LuaValue.Nil;
		}

		public void Set(LuaValue key, LuaValue value)
		{
			if (key.Type == LuaType.Nil) throw new PcosLuaException("table index is nil");
			if (value.Type == LuaType.Nil) Map.Remove(key);
			else Map[key] = value;
		}

		public int Length()
		{
			int n = 0;
			while (true)
			{
				var key = LuaValue.Number(n + 1);
				LuaValue v;
				if (!Map.TryGetValue(key, out v) || v.Type == LuaType.Nil) break;
				n++;
				if (n > 100000) break;
			}
			return n;
		}
	}

	public sealed class LuaFunction
	{
		public List<string> Args;
		public List<Stmt> Body;
		public Env Closure;
		public Func<LuaValue[], LuaValue> Native;
		public bool NativeYield;
	}

	public struct LuaValue : IEquatable<LuaValue>
	{
		public LuaType Type;
		public bool B;
		public double N;
		public string S;
		public LuaTable Table;
		public LuaFunction Fn;

		public static readonly LuaValue Nil = new LuaValue { Type = LuaType.Nil };

		public static LuaValue Bool(bool v) { return new LuaValue { Type = LuaType.Bool, B = v }; }
		public static LuaValue Number(double v) { return new LuaValue { Type = LuaType.Number, N = v }; }
		public static LuaValue String(string v) { return new LuaValue { Type = LuaType.String, S = v ?? "" }; }
		public static LuaValue FromTable(LuaTable t) { return new LuaValue { Type = LuaType.Table, Table = t }; }
		public static LuaValue FromFn(LuaFunction f) { return new LuaValue { Type = LuaType.Function, Fn = f }; }

		public bool IsTruthy()
		{
			if (Type == LuaType.Nil) return false;
			if (Type == LuaType.Bool) return B;
			return true;
		}

		public double AsNumber()
		{
			if (Type == LuaType.Number) return N;
			if (Type == LuaType.String)
			{
				double d;
				if (double.TryParse(S, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
			}
			throw new PcosLuaException("number expected, got " + TypeName());
		}

		public string AsString()
		{
			switch (Type)
			{
				case LuaType.Nil: return "nil";
				case LuaType.Bool: return B ? "true" : "false";
				case LuaType.Number:
					if (Math.Abs(N - Math.Round(N)) < 1e-12 && Math.Abs(N) < 1e15)
						return ((long)Math.Round(N)).ToString(CultureInfo.InvariantCulture);
					return N.ToString("G", CultureInfo.InvariantCulture);
				case LuaType.String: return S ?? "";
				case LuaType.Table: return "table";
				case LuaType.Function: return "function";
				default: return Type.ToString();
			}
		}

		public string TypeName()
		{
			switch (Type)
			{
				case LuaType.Nil: return "nil";
				case LuaType.Bool: return "boolean";
				case LuaType.Number: return "number";
				case LuaType.String: return "string";
				case LuaType.Table: return "table";
				case LuaType.Function: return "function";
				default: return "unknown";
			}
		}

		public bool Equals(LuaValue other)
		{
			if (Type != other.Type) return false;
			switch (Type)
			{
				case LuaType.Nil: return true;
				case LuaType.Bool: return B == other.B;
				case LuaType.Number: return N.Equals(other.N);
				case LuaType.String: return S == other.S;
				case LuaType.Table: return ReferenceEquals(Table, other.Table);
				case LuaType.Function: return ReferenceEquals(Fn, other.Fn);
				default: return false;
			}
		}

		public override bool Equals(object obj) { return obj is LuaValue && Equals((LuaValue)obj); }

		public override int GetHashCode()
		{
			unchecked
			{
				int h = (int)Type * 397;
				switch (Type)
				{
					case LuaType.Bool: return h ^ B.GetHashCode();
					case LuaType.Number: return h ^ N.GetHashCode();
					case LuaType.String: return h ^ (S != null ? S.GetHashCode() : 0);
					case LuaType.Table: return h ^ (Table != null ? Table.GetHashCode() : 0);
					case LuaType.Function: return h ^ (Fn != null ? Fn.GetHashCode() : 0);
					default: return h;
				}
			}
		}

		public override string ToString() { return AsString(); }
	}

	public sealed class Env
	{
		public readonly Dictionary<string, LuaValue> Vars = new Dictionary<string, LuaValue>();
		public readonly Env Parent;

		public Env(Env parent = null) { Parent = parent; }

		public bool TryGet(string name, out LuaValue value)
		{
			if (Vars.TryGetValue(name, out value)) return true;
			if (Parent != null) return Parent.TryGet(name, out value);
			value = LuaValue.Nil;
			return false;
		}

		public LuaValue Get(string name)
		{
			LuaValue v;
			TryGet(name, out v);
			return v;
		}

		public void SetLocal(string name, LuaValue value) { Vars[name] = value; }

		public void Assign(string name, LuaValue value)
		{
			Env e = this;
			while (e != null)
			{
				if (e.Vars.ContainsKey(name))
				{
					e.Vars[name] = value;
					return;
				}
				e = e.Parent;
			}
			Vars[name] = value;
		}
	}

	public abstract class Node { public int Line; }
	public abstract class Expr : Node { }
	public abstract class Stmt : Node { }

	public sealed class LitExpr : Expr { public LuaValue Value; }
	public sealed class NameExpr : Expr { public string Name; }
	public sealed class UnExpr : Expr { public string Op; public Expr A; }
	public sealed class BinExpr : Expr { public string Op; public Expr A, B; }
	public sealed class IndexExpr : Expr { public Expr Table, Key; }
	public sealed class CallExpr : Expr { public Expr Fn; public List<Expr> Args = new List<Expr>(); }
	public sealed class TableExpr : Expr
	{
		public readonly List<Expr> Array = new List<Expr>();
		public readonly List<KeyValuePair<Expr, Expr>> Rec = new List<KeyValuePair<Expr, Expr>>();
	}
	public sealed class FuncExpr : Expr { public List<string> Args = new List<string>(); public List<Stmt> Body = new List<Stmt>(); }

	public sealed class AssignStmt : Stmt { public List<Expr> Targets = new List<Expr>(); public List<Expr> Values = new List<Expr>(); }
	public sealed class CallStmt : Stmt { public CallExpr Call; }
	public sealed class LocalStmt : Stmt { public List<string> Names = new List<string>(); public List<Expr> Values = new List<Expr>(); }
	public sealed class IfStmt : Stmt
	{
		public readonly List<Expr> Conds = new List<Expr>();
		public readonly List<List<Stmt>> Blocks = new List<List<Stmt>>();
		public List<Stmt> ElseBlock;
	}
	public sealed class WhileStmt : Stmt { public Expr Cond; public List<Stmt> Body = new List<Stmt>(); }
	public sealed class RepeatStmt : Stmt { public Expr Cond; public List<Stmt> Body = new List<Stmt>(); }
	public sealed class ForNumStmt : Stmt { public string Name; public Expr From, To, Step; public List<Stmt> Body = new List<Stmt>(); }
	public sealed class FuncStmt : Stmt { public string Name; public Expr Target; public FuncExpr Fn; public bool Local; }
	public sealed class ReturnStmt : Stmt { public List<Expr> Values = new List<Expr>(); }
	public sealed class BreakStmt : Stmt { }
	public sealed class DoStmt : Stmt { public List<Stmt> Body = new List<Stmt>(); }

	public sealed class ReturnSignal : Exception
	{
		public readonly LuaValue Value;
		public ReturnSignal(LuaValue v) { Value = v; }
	}

	public sealed class BreakSignal : Exception { }

	public sealed class PcosLua
	{
		public const int MaxOps = 250000;
		public Env Globals { get; private set; }
		public Action<string> Printer;
		public int Ops;

		public PcosLua()
		{
			Globals = new Env();
			InstallStd();
		}

		public void SetGlobal(string name, LuaValue value) { Globals.SetLocal(name, value); }

		public void SetNative(string name, Func<LuaValue[], LuaValue> fn)
		{
			var f = new LuaFunction { Native = fn };
			Globals.SetLocal(name, LuaValue.FromFn(f));
		}

		public LuaValue DoString(string source)
		{
			Ops = 0;
			var stmts = new Parser(source).Parse();
			try { ExecBlock(stmts, Globals); }
			catch (ReturnSignal r) { return r.Value; }
			return LuaValue.Nil;
		}

		void Tick()
		{
			Ops++;
			if (Ops > MaxOps) throw new PcosLuaException("script ran too long (infinite loop?)");
		}

		void ExecBlock(List<Stmt> block, Env env)
		{
			if (block == null) return;
			for (int i = 0; i < block.Count; i++)
				Exec(block[i], env);
		}

		void Exec(Stmt s, Env env)
		{
			Tick();
			if (s is AssignStmt a)
			{
				var vals = EvalList(a.Values, env);
				for (int i = 0; i < a.Targets.Count; i++)
				{
					var v = i < vals.Count ? vals[i] : LuaValue.Nil;
					Assign(a.Targets[i], v, env);
				}
				return;
			}
			if (s is CallStmt c) { Eval(c.Call, env); return; }
			if (s is LocalStmt loc)
			{
				var vals = EvalList(loc.Values, env);
				for (int i = 0; i < loc.Names.Count; i++)
					env.SetLocal(loc.Names[i], i < vals.Count ? vals[i] : LuaValue.Nil);
				return;
			}
			if (s is IfStmt iff)
			{
				for (int i = 0; i < iff.Conds.Count; i++)
				{
					if (Eval(iff.Conds[i], env).IsTruthy())
					{
						ExecBlock(iff.Blocks[i], new Env(env));
						return;
					}
				}
				if (iff.ElseBlock != null) ExecBlock(iff.ElseBlock, new Env(env));
				return;
			}
			if (s is WhileStmt w)
			{
				int guard = 0;
				while (Eval(w.Cond, env).IsTruthy())
				{
					try { ExecBlock(w.Body, new Env(env)); }
					catch (BreakSignal) { break; }
					if (++guard > 100000) throw new PcosLuaException("while loop too long", w.Line);
				}
				return;
			}
			if (s is RepeatStmt r)
			{
				int guard = 0;
				do
				{
					try { ExecBlock(r.Body, new Env(env)); }
					catch (BreakSignal) { break; }
					if (++guard > 100000) throw new PcosLuaException("repeat loop too long", r.Line);
				} while (!Eval(r.Cond, env).IsTruthy());
				return;
			}
			if (s is ForNumStmt fn)
			{
				double i = Eval(fn.From, env).AsNumber();
				double to = Eval(fn.To, env).AsNumber();
				double step = fn.Step != null ? Eval(fn.Step, env).AsNumber() : 1.0;
				if (Math.Abs(step) < 1e-15) throw new PcosLuaException("'for' step is zero", fn.Line);
				int guard = 0;
				while (step > 0 ? i <= to + 1e-12 : i >= to - 1e-12)
				{
					var inner = new Env(env);
					inner.SetLocal(fn.Name, LuaValue.Number(i));
					try { ExecBlock(fn.Body, inner); }
					catch (BreakSignal) { break; }
					i += step;
					if (++guard > 100000) throw new PcosLuaException("for loop too long", fn.Line);
				}
				return;
			}
			if (s is FuncStmt fs)
			{
				var val = Eval(fs.Fn, env);
				if (fs.Local) env.SetLocal(fs.Name, val);
				else if (fs.Target != null) Assign(fs.Target, val, env);
				else env.Assign(fs.Name, val);
				return;
			}
			if (s is ReturnStmt ret)
			{
				var vals = EvalList(ret.Values, env);
				throw new ReturnSignal(vals.Count > 0 ? vals[0] : LuaValue.Nil);
			}
			if (s is BreakStmt) throw new BreakSignal();
			if (s is DoStmt d) ExecBlock(d.Body, new Env(env));
		}

		void Assign(Expr target, LuaValue value, Env env)
		{
			if (target is NameExpr n) { env.Assign(n.Name, value); return; }
			if (target is IndexExpr ix)
			{
				var t = Eval(ix.Table, env);
				if (t.Type != LuaType.Table) throw new PcosLuaException("attempt to index a " + t.TypeName(), target.Line);
				t.Table.Set(Eval(ix.Key, env), value);
				return;
			}
			throw new PcosLuaException("invalid assignment", target.Line);
		}

		List<LuaValue> EvalList(List<Expr> exprs, Env env)
		{
			var r = new List<LuaValue>();
			if (exprs == null) return r;
			for (int i = 0; i < exprs.Count; i++) r.Add(Eval(exprs[i], env));
			return r;
		}

		LuaValue Eval(Expr e, Env env)
		{
			Tick();
			if (e == null) return LuaValue.Nil;
			if (e is LitExpr l) return l.Value;
			if (e is NameExpr n) return env.Get(n.Name);
			if (e is UnExpr u)
			{
				var a = Eval(u.A, env);
				if (u.Op == "not") return LuaValue.Bool(!a.IsTruthy());
				if (u.Op == "-") return LuaValue.Number(-a.AsNumber());
				if (u.Op == "#")
				{
					if (a.Type == LuaType.String) return LuaValue.Number(a.S.Length);
					if (a.Type == LuaType.Table) return LuaValue.Number(a.Table.Length());
					throw new PcosLuaException("attempt to get length of a " + a.TypeName(), e.Line);
				}
			}
			if (e is BinExpr b) return EvalBin(b, env);
			if (e is IndexExpr ix)
			{
				var t = Eval(ix.Table, env);
				if (t.Type != LuaType.Table) throw new PcosLuaException("attempt to index a " + t.TypeName(), e.Line);
				return t.Table.Get(Eval(ix.Key, env));
			}
			if (e is TableExpr tb)
			{
				var table = new LuaTable();
				int idx = 1;
				for (int i = 0; i < tb.Array.Count; i++)
					table.Set(LuaValue.Number(idx++), Eval(tb.Array[i], env));
				for (int i = 0; i < tb.Rec.Count; i++)
					table.Set(Eval(tb.Rec[i].Key, env), Eval(tb.Rec[i].Value, env));
				return LuaValue.FromTable(table);
			}
			if (e is FuncExpr fe)
			{
				return LuaValue.FromFn(new LuaFunction { Args = fe.Args, Body = fe.Body, Closure = env });
			}
			if (e is CallExpr c) return Call(Eval(c.Fn, env), EvalList(c.Args, env), c.Line);
			throw new PcosLuaException("bad expression", e.Line);
		}

		LuaValue EvalBin(BinExpr b, Env env)
		{
			if (b.Op == "and")
			{
				var l = Eval(b.A, env);
				return l.IsTruthy() ? Eval(b.B, env) : l;
			}
			if (b.Op == "or")
			{
				var l = Eval(b.A, env);
				return l.IsTruthy() ? l : Eval(b.B, env);
			}
			var a = Eval(b.A, env);
			var c = Eval(b.B, env);
			switch (b.Op)
			{
				case "+": return LuaValue.Number(a.AsNumber() + c.AsNumber());
				case "-": return LuaValue.Number(a.AsNumber() - c.AsNumber());
				case "*": return LuaValue.Number(a.AsNumber() * c.AsNumber());
				case "/":
				{
					double divisor = c.AsNumber();
					if (divisor == 0.0) throw new PcosLuaException("attempt to divide by zero", b.Line);
					return LuaValue.Number(a.AsNumber() / divisor);
				}
				case "%":
				{
					double divisor = c.AsNumber();
					if (divisor == 0.0) throw new PcosLuaException("attempt to perform 'n % 0'", b.Line);
					return LuaValue.Number(a.AsNumber() % divisor);
				}
				case "^": return LuaValue.Number(Math.Pow(a.AsNumber(), c.AsNumber()));
				case "..": return LuaValue.String(a.AsString() + c.AsString());
				case "==": return LuaValue.Bool(a.Equals(c));
				case "~=": return LuaValue.Bool(!a.Equals(c));
				case "<": return LuaValue.Bool(Cmp(a, c) < 0);
				case "<=": return LuaValue.Bool(Cmp(a, c) <= 0);
				case ">": return LuaValue.Bool(Cmp(a, c) > 0);
				case ">=": return LuaValue.Bool(Cmp(a, c) >= 0);
			}
			throw new PcosLuaException("unknown operator " + b.Op, b.Line);
		}

		int Cmp(LuaValue a, LuaValue b)
		{
			if (a.Type == LuaType.Number && b.Type == LuaType.Number) return a.N.CompareTo(b.N);
			if (a.Type == LuaType.String && b.Type == LuaType.String) return string.CompareOrdinal(a.S, b.S);
			throw new PcosLuaException("attempt to compare " + a.TypeName() + " with " + b.TypeName());
		}

		public LuaValue Call(LuaValue fn, List<LuaValue> args, int line = 0)
		{
			if (fn.Type != LuaType.Function) throw new PcosLuaException("attempt to call a " + fn.TypeName(), line);
			var f = fn.Fn;
			if (f.Native != null) return f.Native(args != null ? args.ToArray() : new LuaValue[0]);
			var env = new Env(f.Closure ?? Globals);
			if (f.Args != null)
			{
				for (int i = 0; i < f.Args.Count; i++)
					env.SetLocal(f.Args[i], args != null && i < args.Count ? args[i] : LuaValue.Nil);
			}
			try { ExecBlock(f.Body, env); }
			catch (ReturnSignal r) { return r.Value; }
			catch (BreakSignal) { throw new PcosLuaException("break outside loop", line); }
			return LuaValue.Nil;
		}

		void InstallStd()
		{
			SetNative("print", args =>
			{
				var sb = new StringBuilder();
				for (int i = 0; i < args.Length; i++)
				{
					if (i > 0) sb.Append('\t');
					sb.Append(args[i].AsString());
				}
				if (Printer != null) Printer(sb.ToString());
				return LuaValue.Nil;
			});
			SetNative("tonumber", args =>
			{
				if (args.Length == 0) return LuaValue.Nil;
				if (args[0].Type == LuaType.Number) return args[0];
				double d;
				if (args[0].Type == LuaType.String && double.TryParse(args[0].S, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
					return LuaValue.Number(d);
				return LuaValue.Nil;
			});
			SetNative("tostring", args => LuaValue.String(args.Length > 0 ? args[0].AsString() : "nil"));
			SetNative("type", args => LuaValue.String(args.Length > 0 ? args[0].TypeName() : "nil"));
			SetNative("assert", args =>
			{
				if (args.Length == 0 || !args[0].IsTruthy())
					throw new PcosLuaException(args.Length > 1 ? args[1].AsString() : "assertion failed");
				return args[0];
			});
			SetNative("error", args => { throw new PcosLuaException(args.Length > 0 ? args[0].AsString() : "error"); });
			SetNative("select", args =>
			{
				if (args.Length == 0) return LuaValue.Nil;
				if (args[0].Type == LuaType.String && args[0].S == "#") return LuaValue.Number(args.Length - 1);
				int i = (int)args[0].AsNumber();
				if (i < 1 || i >= args.Length) return LuaValue.Nil;
				return args[i];
			});

			// pcall(f, ...) — вызов с защитой от ошибок
			SetNative("pcall", args =>
			{
				if (args.Length == 0 || args[0].Type != LuaType.Function)
					throw new PcosLuaException("pcall: function expected");
				var fn = args[0];
				var callArgs = new List<LuaValue>();
				for (int i = 1; i < args.Length; i++) callArgs.Add(args[i]);
				try
				{
					var result = Call(fn, callArgs);
					var t = new LuaTable();
					t.Set(LuaValue.Number(1), LuaValue.Bool(true));
					t.Set(LuaValue.Number(2), result);
					return LuaValue.FromTable(t);
				}
				catch (PcosLuaException ex)
				{
					var t = new LuaTable();
					t.Set(LuaValue.Number(1), LuaValue.Bool(false));
					t.Set(LuaValue.Number(2), LuaValue.String(ex.Message));
					return LuaValue.FromTable(t);
				}
				catch (Exception ex)
				{
					var t = new LuaTable();
					t.Set(LuaValue.Number(1), LuaValue.Bool(false));
					t.Set(LuaValue.Number(2), LuaValue.String(ex.Message));
					return LuaValue.FromTable(t);
				}
			});

			// pairs(t) — возвращает итератор, таблицу, nil
			SetNative("pairs", args =>
			{
				if (args.Length == 0 || args[0].Type != LuaType.Table)
					throw new PcosLuaException("pairs: table expected, got " + (args.Length > 0 ? args[0].TypeName() : "no value"));
				var tbl = args[0].Table;
				// Создаём closure-итератор через нативную функцию
				// Для простоты возвращаем таблицу ключей и счётчик
				var keys = new List<LuaValue>();
				foreach (var kv in tbl.Map) keys.Add(kv.Key);
				int[] idx = { 0 };
				var iterFn = new LuaFunction
				{
					Native = a =>
					{
							while (idx[0] < keys.Count)
							{
								var k = keys[idx[0]++];
								var v = tbl.Get(k);
								if (v.Type != LuaType.Nil)
								{
									var pair = new LuaTable();
									pair.Set(LuaValue.Number(1), k);
									pair.Set(LuaValue.Number(2), v);
									return LuaValue.FromTable(pair);
								}
							}
						return LuaValue.Nil;
					}
				};
				// Возвращаем: iterator function, table, nil
				// Но поскольку у нас нет multi-return, меняем подход:
				// pairs(t) возвращает таблицу {keys={k1,k2,...}, values={v1,v2,...}}
				// и пользователь итерирует через for i=1, #result.keys do
				var keysTable = new LuaTable();
				var valsTable = new LuaTable();
				int n = 1;
				foreach (var kv in tbl.Map)
				{
					keysTable.Set(LuaValue.Number(n), kv.Key);
					valsTable.Set(LuaValue.Number(n), kv.Value);
					n++;
				}
				var result = new LuaTable();
				result.Set(LuaValue.String("keys"), LuaValue.FromTable(keysTable));
				result.Set(LuaValue.String("values"), LuaValue.FromTable(valsTable));
				result.Set(LuaValue.String("n"), LuaValue.Number(n - 1));
				return LuaValue.FromTable(result);
			});

			// ipairs(t) — итерация по числовым ключам 1..n
			SetNative("ipairs", args =>
			{
				if (args.Length == 0 || args[0].Type != LuaType.Table)
					throw new PcosLuaException("ipairs: table expected, got " + (args.Length > 0 ? args[0].TypeName() : "no value"));
				var tbl = args[0].Table;
				int len = tbl.Length();
				var result = new LuaTable();
				result.Set(LuaValue.String("n"), LuaValue.Number(len));
				// Просто возвращаем таблицу (массив уже есть)
				return LuaValue.FromTable(tbl);
			});

			var math = new LuaTable();
			math.Set(LuaValue.String("pi"), LuaValue.Number(Math.PI));
			math.Set(LuaValue.String("abs"), Native(a => LuaValue.Number(Math.Abs(Num(a, 0)))));
			math.Set(LuaValue.String("floor"), Native(a => LuaValue.Number(Math.Floor(Num(a, 0)))));
			math.Set(LuaValue.String("ceil"), Native(a => LuaValue.Number(Math.Ceiling(Num(a, 0)))));
			math.Set(LuaValue.String("sqrt"), Native(a => LuaValue.Number(Math.Sqrt(Num(a, 0)))));
			math.Set(LuaValue.String("sin"), Native(a => LuaValue.Number(Math.Sin(Num(a, 0)))));
			math.Set(LuaValue.String("cos"), Native(a => LuaValue.Number(Math.Cos(Num(a, 0)))));
			math.Set(LuaValue.String("min"), Native(a =>
			{
				double m = a.Length == 0 ? 0 : Num(a, 0);
				for (int i = 1; i < a.Length; i++) m = Math.Min(m, Num(a, i));
				return LuaValue.Number(m);
			}));
			math.Set(LuaValue.String("max"), Native(a =>
			{
				double m = a.Length == 0 ? 0 : Num(a, 0);
				for (int i = 1; i < a.Length; i++) m = Math.Max(m, Num(a, i));
				return LuaValue.Number(m);
			}));
			math.Set(LuaValue.String("random"), Native(a =>
			{
				if (a.Length == 0) return LuaValue.Number(UnityEngine.Random.value);
				if (a.Length == 1) return LuaValue.Number(UnityEngine.Random.Range(1, (int)Num(a, 0) + 1));
				return LuaValue.Number(UnityEngine.Random.Range((int)Num(a, 0), (int)Num(a, 1) + 1));
			}));
			math.Set(LuaValue.String("huge"), LuaValue.Number(double.PositiveInfinity));
			math.Set(LuaValue.String("maxinteger"), LuaValue.Number(double.MaxValue));
			math.Set(LuaValue.String("mininteger"), LuaValue.Number(double.MinValue));
			SetGlobal("math", LuaValue.FromTable(math));

			var str = new LuaTable();
			str.Set(LuaValue.String("len"), Native(a => LuaValue.Number(Str(a, 0).Length)));
			str.Set(LuaValue.String("upper"), Native(a => LuaValue.String(Str(a, 0).ToUpperInvariant())));
			str.Set(LuaValue.String("lower"), Native(a => LuaValue.String(Str(a, 0).ToLowerInvariant())));
			str.Set(LuaValue.String("rep"), Native(a =>
			{
				var s = Str(a, 0);
				int n = a.Length > 1 ? (int)Num(a, 1) : 0;
				if (n < 0) n = 0;
				if (n > 10000) n = 10000;
				var sb = new StringBuilder();
				for (int i = 0; i < n; i++) sb.Append(s);
				return LuaValue.String(sb.ToString());
			}));
			str.Set(LuaValue.String("sub"), Native(a =>
			{
				var s = Str(a, 0);
				int i = a.Length > 1 ? (int)Num(a, 1) : 1;
				int j = a.Length > 2 ? (int)Num(a, 2) : s.Length;
				if (i < 0) i = s.Length + i + 1;
				if (j < 0) j = s.Length + j + 1;
				if (i < 1) i = 1;
				if (j > s.Length) j = s.Length;
				if (j < i) return LuaValue.String("");
				return LuaValue.String(s.Substring(i - 1, j - i + 1));
			}));
			str.Set(LuaValue.String("find"), Native(a =>
			{
				var s = Str(a, 0);
				var p = a.Length > 1 ? a[1].AsString() : "";
				int start = a.Length > 2 ? (int)Num(a, 2) : 1;
				if (start < 1) start = 1;
				if (start > s.Length) return LuaValue.Nil;
				int idx = s.IndexOf(p, start - 1, StringComparison.Ordinal);
				return idx < 0 ? LuaValue.Nil : LuaValue.Number(idx + 1);
			}));
			str.Set(LuaValue.String("reverse"), Native(a =>
			{
				var s = Str(a, 0);
				var chars = s.ToCharArray();
				Array.Reverse(chars);
				return LuaValue.String(new string(chars));
			}));
			str.Set(LuaValue.String("byte"), Native(a =>
			{
				var s = Str(a, 0);
				int i = a.Length > 1 ? (int)Num(a, 1) : 1;
				if (i < 1 || i > s.Length) return LuaValue.Nil;
				return LuaValue.Number((int)s[i - 1]);
			}));
			str.Set(LuaValue.String("char"), Native(a =>
			{
				var sb = new StringBuilder();
				for (int i = 0; i < a.Length; i++)
				{
					int code = (int)Num(a, i);
					if (code < 0 || code > 0xFFFF) throw new PcosLuaException("invalid value for string.char");
					sb.Append((char)code);
				}
				return LuaValue.String(sb.ToString());
			}));
			str.Set(LuaValue.String("format"), Native(a =>
			{
				if (a.Length == 0) return LuaValue.String("");
				var fmt = Str(a, 0);
				try { return LuaValue.String(LuaStringFormat(fmt, a)); }
				catch { return LuaValue.String(fmt); }
			}));
			SetGlobal("string", LuaValue.FromTable(str));

			var tbl = new LuaTable();
			tbl.Set(LuaValue.String("insert"), Native(a =>
			{
				if (a.Length < 2 || a[0].Type != LuaType.Table) return LuaValue.Nil;
				var t = a[0].Table;
				t.Set(LuaValue.Number(t.Length() + 1), a[1]);
				return LuaValue.Nil;
			}));
			tbl.Set(LuaValue.String("concat"), Native(a =>
			{
				if (a.Length == 0 || a[0].Type != LuaType.Table) return LuaValue.String("");
				var sep = a.Length > 1 ? a[1].AsString() : "";
				var t = a[0].Table;
				int n = t.Length();
				var sb = new StringBuilder();
				for (int i = 1; i <= n; i++)
				{
					if (i > 1) sb.Append(sep);
					sb.Append(t.Get(LuaValue.Number(i)).AsString());
				}
				return LuaValue.String(sb.ToString());
			}));
			tbl.Set(LuaValue.String("remove"), Native(a =>
			{
				if (a.Length == 0 || a[0].Type != LuaType.Table) return LuaValue.Nil;
				var t = a[0].Table;
				int n = t.Length();
				if (n == 0) return LuaValue.Nil;
				int pos = a.Length > 1 ? (int)Num(a, 1) : n;
				if (pos < 1) pos = 1;
				if (pos > n) pos = n;
				var removed = t.Get(LuaValue.Number(pos));
				// Сдвигаем элементы
				for (int i = pos; i < n; i++)
					t.Set(LuaValue.Number(i), t.Get(LuaValue.Number(i + 1)));
				t.Set(LuaValue.Number(n), LuaValue.Nil); // удаляем последний
				return removed;
			}));
			tbl.Set(LuaValue.String("sort"), Native(a =>
			{
				if (a.Length == 0 || a[0].Type != LuaType.Table) return LuaValue.Nil;
				var t = a[0].Table;
				int n = t.Length();
				if (n <= 1) return LuaValue.Nil;
				var arr = new LuaValue[n];
				for (int i = 0; i < n; i++) arr[i] = t.Get(LuaValue.Number(i + 1));
				bool hasFn = a.Length > 1 && a[1].Type == LuaType.Function;
				var cmpFn = hasFn ? a[1].Fn : null;
				var self = this;
				Array.Sort(arr, (x, y) =>
				{
					if (cmpFn != null)
					{
						try
						{
							var r = self.Call(LuaValue.FromFn(cmpFn), new List<LuaValue> { x, y });
							return r.IsTruthy() ? -1 : 1;
						}
						catch { return 0; }
					}
					if (x.Type == LuaType.Number && y.Type == LuaType.Number)
						return x.N.CompareTo(y.N);
					if (x.Type == LuaType.String && y.Type == LuaType.String)
						return string.CompareOrdinal(x.S, y.S);
					return 0;
				});
				for (int i = 0; i < n; i++) t.Set(LuaValue.Number(i + 1), arr[i]);
				return LuaValue.Nil;
			}));
			SetGlobal("table", LuaValue.FromTable(tbl));
		}

		static double Num(LuaValue[] a, int i) { return i < a.Length ? a[i].AsNumber() : 0; }
		static string Str(LuaValue[] a, int i) { return i < a.Length ? a[i].AsString() : ""; }
		static LuaValue Native(Func<LuaValue[], LuaValue> fn) { return LuaValue.FromFn(new LuaFunction { Native = fn }); }

		static string LuaStringFormat(string fmt, LuaValue[] args)
		{
			var sb = new StringBuilder();
			int ai = 1;
			for (int i = 0; i < fmt.Length; i++)
			{
				if (fmt[i] == '%' && i + 1 < fmt.Length)
				{
					if (fmt[i + 1] == '%') { sb.Append('%'); i++; continue; }
					i++;
					var spec = new StringBuilder("%");
					// flags
					while (i < fmt.Length && "-+ #0".IndexOf(fmt[i]) >= 0) { spec.Append(fmt[i]); i++; }
					// width
					while (i < fmt.Length && char.IsDigit(fmt[i])) { spec.Append(fmt[i]); i++; }
					// precision
					if (i < fmt.Length && fmt[i] == '.')
					{
						spec.Append('.'); i++;
						while (i < fmt.Length && char.IsDigit(fmt[i])) { spec.Append(fmt[i]); i++; }
					}
					if (i >= fmt.Length) break;
					char conv = fmt[i];
					spec.Append(conv);
					if (ai >= args.Length) { sb.Append(spec); continue; }
					var v = args[ai++];
					switch (conv)
					{
						case 'd': case 'i':
							sb.Append(string.Format(spec.ToString().Replace("d", "D").Replace("i", "D"), (long)v.AsNumber()));
							break;
						case 'f':
							sb.Append(string.Format(CultureInfo.InvariantCulture, spec.ToString(), v.AsNumber()));
							break;
						case 'e': case 'E':
							sb.Append(v.AsNumber().ToString(spec.ToString(), CultureInfo.InvariantCulture));
							break;
						case 'x': case 'X':
							sb.Append(((long)v.AsNumber()).ToString(spec.ToString().Replace("x", "x").Replace("X", "X"), CultureInfo.InvariantCulture));
							break;
						case 'o':
							sb.Append(Convert.ToString((long)v.AsNumber(), 8));
							break;
						case 's':
							sb.Append(v.AsString());
							break;
						case 'c':
							sb.Append((char)(int)v.AsNumber());
							break;
						case 'q':
							sb.Append('"').Append(v.AsString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")).Append('"');
							break;
						default:
							sb.Append(spec);
							break;
					}
				}
				else sb.Append(fmt[i]);
			}
			return sb.ToString();
		}
	}

	sealed class Tok
	{
		public string Kind;
		public string Text;
		public double Num;
		public int Line;
	}

	sealed class Lexer
	{
		readonly string s;
		int i, line = 1;
		public Lexer(string src) { s = src ?? ""; }

		public List<Tok> Run()
		{
			var r = new List<Tok>();
			while (true)
			{
				var t = Next();
				r.Add(t);
				if (t.Kind == "eof") break;
			}
			return r;
		}

		Tok Next()
		{
			Skip();
			if (i >= s.Length) return T("eof", "");
			char c = s[i];
			if (char.IsLetter(c) || c == '_')
			{
				int a = i;
				i++;
				while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
				var w = s.Substring(a, i - a);
				return T(Kw(w) ? w : "name", w);
			}
			if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
				return Num();
			if (c == '"' || c == '\'') return Str(c);
			if (c == '[' && i + 1 < s.Length && s[i + 1] == '[') return LongStr();
			if (c == '.' && i + 1 < s.Length && s[i + 1] == '.')
			{
				if (i + 2 < s.Length && s[i + 2] == '.') { i += 3; return T("...", "..."); }
				i += 2; return T("..", "..");
			}
			if (c == '=' || c == '~' || c == '<' || c == '>')
			{
				if (i + 1 < s.Length && s[i + 1] == '=')
				{
					var op = new string(new[] { c, '=' });
					i += 2; return T(op, op);
				}
			}
			i++;
			var one = c.ToString();
			return T(one, one);
		}

		void Skip()
		{
			while (i < s.Length)
			{
				char c = s[i];
				if (c == ' ' || c == '\t' || c == '\r') { i++; continue; }
				if (c == '\n') { i++; line++; continue; }
				if (c == '-' && i + 1 < s.Length && s[i + 1] == '-')
				{
					i += 2;
					if (i + 1 < s.Length && s[i] == '[' && s[i + 1] == '[')
					{
						i += 2;
						while (i + 1 < s.Length && !(s[i] == ']' && s[i + 1] == ']'))
						{
							if (s[i] == '\n') line++;
							i++;
						}
						if (i + 1 < s.Length) i += 2;
					}
					else
					{
						while (i < s.Length && s[i] != '\n') i++;
					}
					continue;
				}
				break;
			}
		}

		Tok Num()
		{
			int a = i;
			while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-'))
			{
				if ((s[i] == '+' || s[i] == '-') && i > a && s[i - 1] != 'e' && s[i - 1] != 'E') break;
				i++;
			}
			var t = s.Substring(a, i - a);
			double d;
			if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
				throw new PcosLuaException("bad number " + t, line);
			return new Tok { Kind = "number", Text = t, Num = d, Line = line };
		}

		Tok Str(char q)
		{
			i++;
			var sb = new StringBuilder();
			while (i < s.Length && s[i] != q)
			{
				if (s[i] == '\\' && i + 1 < s.Length)
				{
					i++;
					char e = s[i++];
					if (e == 'n') sb.Append('\n');
					else if (e == 't') sb.Append('\t');
					else if (e == 'r') sb.Append('\r');
					else sb.Append(e);
					continue;
				}
				if (s[i] == '\n') line++;
				sb.Append(s[i++]);
			}
			if (i >= s.Length) throw new PcosLuaException("unfinished string", line);
			i++;
			return new Tok { Kind = "string", Text = sb.ToString(), Line = line };
		}

		Tok LongStr()
		{
			int startLine = line;
			i += 2; // skip [[
			var sb = new StringBuilder();
			// Skip immediate newline after [[
			if (i < s.Length && s[i] == '\n') { i++; line++; }
			else if (i < s.Length && s[i] == '\r')
			{
				i++;
				if (i < s.Length && s[i] == '\n') { i++; }
				line++;
			}
			while (i + 1 < s.Length && !(s[i] == ']' && s[i + 1] == ']'))
			{
				if (s[i] == '\n') line++;
				sb.Append(s[i++]);
			}
			if (i + 1 >= s.Length) throw new PcosLuaException("unfinished long string", startLine);
			i += 2; // skip ]]
			return new Tok { Kind = "string", Text = sb.ToString(), Line = startLine };
		}

		Tok T(string k, string t) { return new Tok { Kind = k, Text = t, Line = line }; }

		static bool Kw(string w)
		{
			switch (w)
			{
				case "and": case "break": case "do": case "else": case "elseif":
				case "end": case "false": case "for": case "function": case "if":
				case "in": case "local": case "nil": case "not": case "or":
				case "repeat": case "return": case "then": case "true": case "until":
				case "while": return true;
				default: return false;
			}
		}
	}

	sealed class Parser
	{
		readonly List<Tok> ts;
		int p;

		public Parser(string src) { ts = new Lexer(src).Run(); }

		Tok Cur { get { return ts[p]; } }
		bool Is(string k) { return Cur.Kind == k; }
		Tok Eat(string k)
		{
			if (!Is(k)) throw new PcosLuaException("expected " + k + ", got " + Cur.Kind, Cur.Line);
			var t = Cur; p++; return t;
		}
		bool EatIf(string k) { if (!Is(k)) return false; p++; return true; }

		public List<Stmt> Parse()
		{
			var b = Block();
			if (!Is("eof")) throw new PcosLuaException("unexpected " + Cur.Kind, Cur.Line);
			return b;
		}

		List<Stmt> Block()
		{
			var r = new List<Stmt>();
			while (!Is("eof") && !Is("end") && !Is("else") && !Is("elseif") && !Is("until"))
			{
				if (Is("return")) { r.Add(Ret()); break; }
				r.Add(Statement());
			}
			return r;
		}

		Stmt Statement()
		{
			int line = Cur.Line;
			if (EatIf(";")) return new DoStmt { Body = new List<Stmt>(), Line = line };
			if (Is("if")) return If();
			if (Is("while")) return While();
			if (Is("repeat")) return Repeat();
			if (Is("for")) return For();
			if (Is("function")) return Func(false);
			if (Is("local"))
			{
				p++;
				if (Is("function")) return Func(true);
				return Local();
			}
			if (Is("do"))
			{
				p++;
				var body = Block();
				Eat("end");
				return new DoStmt { Body = body, Line = line };
			}
			if (Is("break")) { p++; return new BreakStmt { Line = line }; }
			if (Is("return")) return Ret();

			var e = Prefix();
			if (Is("=") || Is(","))
			{
				var targets = new List<Expr> { e };
				while (EatIf(",")) targets.Add(Prefix());
				Eat("=");
				var vals = Explist();
				return new AssignStmt { Targets = targets, Values = vals, Line = line };
			}
			if (e is CallExpr call) return new CallStmt { Call = call, Line = line };
			throw new PcosLuaException("unexpected statement", line);
		}

		Stmt If()
		{
			var s = new IfStmt { Line = Cur.Line };
			Eat("if");
			s.Conds.Add(Exp());
			Eat("then");
			s.Blocks.Add(Block());
			while (EatIf("elseif"))
			{
				s.Conds.Add(Exp());
				Eat("then");
				s.Blocks.Add(Block());
			}
			if (EatIf("else")) s.ElseBlock = Block();
			Eat("end");
			return s;
		}

		Stmt While()
		{
			var s = new WhileStmt { Line = Cur.Line };
			Eat("while");
			s.Cond = Exp();
			Eat("do");
			s.Body = Block();
			Eat("end");
			return s;
		}

		Stmt Repeat()
		{
			var s = new RepeatStmt { Line = Cur.Line };
			Eat("repeat");
			s.Body = Block();
			Eat("until");
			s.Cond = Exp();
			return s;
		}

		Stmt For()
		{
			int line = Cur.Line;
			Eat("for");
			var name = Eat("name").Text;
			if (Is("in")) throw new PcosLuaException("generic for is not supported, use numeric for", line);
			Eat("=");
			var from = Exp();
			Eat(",");
			var to = Exp();
			Expr step = null;
			if (EatIf(",")) step = Exp();
			Eat("do");
			var body = Block();
			Eat("end");
			return new ForNumStmt { Name = name, From = from, To = to, Step = step, Body = body, Line = line };
		}

		Stmt Func(bool local)
		{
			int line = Cur.Line;
			Eat("function");
			var name = Eat("name").Text;
			Expr target = new NameExpr { Name = name, Line = line };
			string last = name;
			while (EatIf("."))
			{
				var field = Eat("name").Text;
				target = new IndexExpr { Table = target, Key = new LitExpr { Value = LuaValue.String(field), Line = line }, Line = line };
				last = field;
			}
			var fn = FuncBody();
			fn.Line = line;
			return new FuncStmt { Name = last, Target = local ? null : target, Fn = fn, Local = local, Line = line };
		}

		FuncExpr FuncBody()
		{
			var f = new FuncExpr { Line = Cur.Line };
			Eat("(");
			if (!Is(")"))
			{
				f.Args.Add(Eat("name").Text);
				while (EatIf(",")) f.Args.Add(Eat("name").Text);
			}
			Eat(")");
			f.Body = Block();
			Eat("end");
			return f;
		}

		Stmt Local()
		{
			var s = new LocalStmt { Line = Cur.Line };
			s.Names.Add(Eat("name").Text);
			while (EatIf(",")) s.Names.Add(Eat("name").Text);
			if (EatIf("=")) s.Values = Explist();
			return s;
		}

		Stmt Ret()
		{
			var s = new ReturnStmt { Line = Cur.Line };
			Eat("return");
			if (!Is("end") && !Is("else") && !Is("elseif") && !Is("until") && !Is("eof") && !Is(";"))
				s.Values = Explist();
			EatIf(";");
			return s;
		}

		List<Expr> Explist()
		{
			var r = new List<Expr> { Exp() };
			while (EatIf(",")) r.Add(Exp());
			return r;
		}

		Expr Exp() { return ExpOr(); }
		Expr ExpOr() { var e = ExpAnd(); while (Is("or")) { int line = Cur.Line; p++; e = new BinExpr { Op = "or", A = e, B = ExpAnd(), Line = line }; } return e; }
		Expr ExpAnd() { var e = ExpCmp(); while (Is("and")) { int line = Cur.Line; p++; e = new BinExpr { Op = "and", A = e, B = ExpCmp(), Line = line }; } return e; }

		Expr ExpCmp()
		{
			var e = ExpConcat();
			while (Is("<") || Is(">") || Is("<=") || Is(">=") || Is("~=") || Is("=="))
			{
				var op = Cur.Kind; int line = Cur.Line; p++;
				e = new BinExpr { Op = op, A = e, B = ExpConcat(), Line = line };
			}
			return e;
		}

		Expr ExpConcat()
		{
			var e = ExpAdd();
			if (Is(".."))
			{
				int line = Cur.Line; p++;
				e = new BinExpr { Op = "..", A = e, B = ExpConcat(), Line = line };
			}
			return e;
		}

		Expr ExpAdd()
		{
			var e = ExpMul();
			while (Is("+") || Is("-"))
			{
				var op = Cur.Kind; int line = Cur.Line; p++;
				e = new BinExpr { Op = op, A = e, B = ExpMul(), Line = line };
			}
			return e;
		}

		Expr ExpMul()
		{
			var e = ExpUn();
			while (Is("*") || Is("/") || Is("%"))
			{
				var op = Cur.Kind; int line = Cur.Line; p++;
				e = new BinExpr { Op = op, A = e, B = ExpUn(), Line = line };
			}
			return e;
		}

		Expr ExpUn()
		{
			if (Is("not") || Is("#") || Is("-"))
			{
				var op = Cur.Kind; int line = Cur.Line; p++;
				return new UnExpr { Op = op, A = ExpUn(), Line = line };
			}
			return ExpPow();
		}

		Expr ExpPow()
		{
			var e = Atom();
			if (Is("^"))
			{
				int line = Cur.Line; p++;
				e = new BinExpr { Op = "^", A = e, B = ExpUn(), Line = line };
			}
			return e;
		}

		Expr Atom()
		{
			int line = Cur.Line;
			if (Is("nil")) { p++; return new LitExpr { Value = LuaValue.Nil, Line = line }; }
			if (Is("true")) { p++; return new LitExpr { Value = LuaValue.Bool(true), Line = line }; }
			if (Is("false")) { p++; return new LitExpr { Value = LuaValue.Bool(false), Line = line }; }
			if (Is("number")) { var t = Eat("number"); return new LitExpr { Value = LuaValue.Number(t.Num), Line = line }; }
			if (Is("string")) { var t = Eat("string"); return new LitExpr { Value = LuaValue.String(t.Text), Line = line }; }
			if (Is("function")) { p++; return FuncBody(); }
			if (Is("{")) return Table();
			if (Is("(")) { p++; var e = Exp(); Eat(")"); return Suffix(e); }
			if (Is("name")) return Suffix(new NameExpr { Name = Eat("name").Text, Line = line });
			throw new PcosLuaException("unexpected " + Cur.Kind, line);
		}

		Expr Prefix()
		{
			int line = Cur.Line;
			if (Is("(")) { p++; var e = Exp(); Eat(")"); return Suffix(e); }
			if (Is("name")) return Suffix(new NameExpr { Name = Eat("name").Text, Line = line });
			throw new PcosLuaException("expected name", line);
		}

		Expr Suffix(Expr e)
		{
			while (true)
			{
				int line = Cur.Line;
				if (EatIf("."))
				{
					var n = Eat("name").Text;
					e = new IndexExpr { Table = e, Key = new LitExpr { Value = LuaValue.String(n), Line = line }, Line = line };
					continue;
				}
				if (EatIf("["))
				{
					var k = Exp();
					Eat("]");
					e = new IndexExpr { Table = e, Key = k, Line = line };
					continue;
				}
				if (Is("(") || Is("string") || Is("{"))
				{
					var c = new CallExpr { Fn = e, Line = line };
					if (Is("string")) c.Args.Add(new LitExpr { Value = LuaValue.String(Eat("string").Text), Line = line });
					else if (Is("{")) c.Args.Add(Table());
					else
					{
						Eat("(");
						if (!Is(")")) c.Args.AddRange(Explist());
						Eat(")");
					}
					e = c;
					continue;
				}
				break;
			}
			return e;
		}

		TableExpr Table()
		{
			var t = new TableExpr { Line = Cur.Line };
			Eat("{");
			while (!Is("}"))
			{
				if (Is("[") )
				{
					p++;
					var k = Exp();
					Eat("]");
					Eat("=");
					t.Rec.Add(new KeyValuePair<Expr, Expr>(k, Exp()));
				}
				else if (Is("name") && p + 1 < ts.Count && ts[p + 1].Kind == "=")
				{
					var n = Eat("name");
					Eat("=");
					var key = new LitExpr { Value = LuaValue.String(n.Text), Line = n.Line };
					t.Rec.Add(new KeyValuePair<Expr, Expr>(key, Exp()));
				}
				else t.Array.Add(Exp());
				if (!EatIf(",") && !EatIf(";")) break;
			}
			Eat("}");
			return t;
		}
	}
}
