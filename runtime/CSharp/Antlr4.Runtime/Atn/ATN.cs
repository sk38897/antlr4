/*
 * [The "BSD license"]
 *  Copyright (c) 2013 Terence Parr
 *  Copyright (c) 2013 Sam Harwell
 *  All rights reserved.
 *
 *  Redistribution and use in source and binary forms, with or without
 *  modification, are permitted provided that the following conditions
 *  are met:
 *
 *  1. Redistributions of source code must retain the above copyright
 *     notice, this list of conditions and the following disclaimer.
 *  2. Redistributions in binary form must reproduce the above copyright
 *     notice, this list of conditions and the following disclaimer in the
 *     documentation and/or other materials provided with the distribution.
 *  3. The name of the author may not be used to endorse or promote products
 *     derived from this software without specific prior written permission.
 *
 *  THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 *  IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 *  OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 *  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 *  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 *  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 *  DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 *  THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 *  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 *  THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Misc;
using Sharpen;

namespace Antlr4.Runtime.Atn
{
    public class ATN
    {
        public const int InvalidAltNumber = 0;

        [NotNull]
        public readonly IList<ATNState> states = new List<ATNState>();

        /// <summary>
        /// Each subrule/rule is a decision point and we must track them so we
        /// can go back later and build DFA predictors for them.
        /// </summary>
        /// <remarks>
        /// Each subrule/rule is a decision point and we must track them so we
        /// can go back later and build DFA predictors for them.  This includes
        /// all the rules, subrules, optional blocks, ()+, ()* etc...
        /// </remarks>
        [NotNull]
        public readonly IList<DecisionState> decisionToState = new List<DecisionState>();

        /// <summary>Maps from rule index to starting state number.</summary>
        /// <remarks>Maps from rule index to starting state number.</remarks>
        public RuleStartState[] ruleToStartState;

        /// <summary>Maps from rule index to stop state number.</summary>
        /// <remarks>Maps from rule index to stop state number.</remarks>
        public RuleStopState[] ruleToStopState;

        [NotNull]
        public readonly IDictionary<string, TokensStartState> modeNameToStartState = new Dictionary<string, TokensStartState>();

        /// <summary>The type of the ATN.</summary>
        /// <remarks>The type of the ATN.</remarks>
        public readonly ATNType grammarType;

        /// <summary>The maximum value for any symbol recognized by a transition in the ATN.</summary>
        /// <remarks>The maximum value for any symbol recognized by a transition in the ATN.</remarks>
        public readonly int maxTokenType;

        /// <summary>For lexer ATNs, this maps the rule index to the resulting token type.</summary>
        /// <remarks>
        /// For lexer ATNs, this maps the rule index to the resulting token type.
        /// <p/>
        /// This is
        /// <code>null</code>
        /// for parser ATNs.
        /// </remarks>
        public int[] ruleToTokenType;

        /// <summary>
        /// For lexer ATNs, this maps the rule index to the action which should be
        /// executed following a match.
        /// </summary>
        /// <remarks>
        /// For lexer ATNs, this maps the rule index to the action which should be
        /// executed following a match.
        /// <p/>
        /// This is
        /// <code>null</code>
        /// for parser ATNs.
        /// </remarks>
        public int[] ruleToActionIndex;

        [NotNull]
        public readonly IList<TokensStartState> modeToStartState = new List<TokensStartState>();

        private readonly ConcurrentDictionary<PredictionContext, PredictionContext> contextCache = new ConcurrentDictionary<PredictionContext, PredictionContext>();

        [NotNull]
        public DFA[] decisionToDFA = new DFA[0];

        [NotNull]
        public DFA[] modeToDFA = new DFA[0];

        protected internal readonly ConcurrentDictionary<int, int> LL1Table = new ConcurrentDictionary<int, int>();

        /// <summary>Used for runtime deserialization of ATNs from strings</summary>
        public ATN(ATNType grammarType, int maxTokenType)
        {
            this.grammarType = grammarType;
            this.maxTokenType = maxTokenType;
        }

        public void ClearDFA()
        {
            decisionToDFA = new DFA[decisionToState.Count];
            for (int i = 0; i < decisionToDFA.Length; i++)
            {
                decisionToDFA[i] = new DFA(decisionToState[i], i);
            }
            modeToDFA = new DFA[modeToStartState.Count];
            for (int i_1 = 0; i_1 < modeToDFA.Length; i_1++)
            {
                modeToDFA[i_1] = new DFA(modeToStartState[i_1]);
            }
            contextCache.Clear();
            LL1Table.Clear();
        }

        public virtual int GetContextCacheSize()
        {
            return contextCache.Count;
        }

        public virtual PredictionContext GetCachedContext(PredictionContext context)
        {
            return PredictionContext.GetCachedContext(context, contextCache, new PredictionContext.IdentityHashMap());
        }

        public DFA[] GetDecisionToDFA()
        {
            System.Diagnostics.Debug.Assert(decisionToDFA != null && decisionToDFA.Length == decisionToState.Count);
            return decisionToDFA;
        }

        /// <summary>
        /// Compute the set of valid tokens that can occur starting in state
        /// <code>s</code>
        /// .
        /// If
        /// <code>ctx</code>
        /// is
        /// <see cref="PredictionContext.EmptyLocal">PredictionContext.EmptyLocal</see>
        /// , the set of tokens will not include what can follow
        /// the rule surrounding
        /// <code>s</code>
        /// . In other words, the set will be
        /// restricted to tokens reachable staying within
        /// <code>s</code>
        /// 's rule.
        /// </summary>
        [return: NotNull]
        public virtual IntervalSet NextTokens(ATNState s, PredictionContext ctx)
        {
            Args.NotNull("ctx", ctx);
            LL1Analyzer anal = new LL1Analyzer(this);
            IntervalSet next = anal.Look(s, ctx);
            return next;
        }

        /// <summary>
        /// Compute the set of valid tokens that can occur starting in
        /// <code>s</code>
        /// and
        /// staying in same rule.
        /// <see cref="TokenConstants.Epsilon"/>
        /// is in set if we reach end of
        /// rule.
        /// </summary>
        [return: NotNull]
        public virtual IntervalSet NextTokens(ATNState s)
        {
            if (s.nextTokenWithinRule != null)
            {
                return s.nextTokenWithinRule;
            }
            s.nextTokenWithinRule = NextTokens(s, PredictionContext.EmptyLocal);
            s.nextTokenWithinRule.SetReadonly(true);
            return s.nextTokenWithinRule;
        }

        public virtual void AddState(ATNState state)
        {
            if (state != null)
            {
                state.atn = this;
                state.stateNumber = states.Count;
            }
            states.Add(state);
        }

        public virtual void RemoveState(ATNState state)
        {
            states[state.stateNumber] = null;
        }

        // just free mem, don't shift states in list
        public virtual void DefineMode(string name, TokensStartState s)
        {
            modeNameToStartState[name] = s;
            modeToStartState.Add(s);
            modeToDFA = Arrays.CopyOf(modeToDFA, modeToStartState.Count);
            modeToDFA[modeToDFA.Length - 1] = new DFA(s);
            DefineDecisionState(s);
        }

        public virtual int DefineDecisionState(DecisionState s)
        {
            decisionToState.Add(s);
            s.decision = decisionToState.Count - 1;
            decisionToDFA = Arrays.CopyOf(decisionToDFA, decisionToState.Count);
            decisionToDFA[decisionToDFA.Length - 1] = new DFA(s, s.decision);
            return s.decision;
        }

        public virtual DecisionState GetDecisionState(int decision)
        {
            if (decisionToState.Count != 0)
            {
                return decisionToState[decision];
            }
            return null;
        }

        public virtual int GetNumberOfDecisions()
        {
            return decisionToState.Count;
        }

        /// <summary>
        /// Computes the set of input symbols which could follow ATN state number
        /// <code>stateNumber</code>
        /// in the specified full
        /// <code>context</code>
        /// . This method
        /// considers the complete parser context, but does not evaluate semantic
        /// predicates (i.e. all predicates encountered during the calculation are
        /// assumed true). If a path in the ATN exists from the starting state to the
        /// <see cref="RuleStopState">RuleStopState</see>
        /// of the outermost context without matching any
        /// symbols,
        /// <see cref="TokenConstants.Eof"/>
        /// is added to the returned set.
        /// <p/>
        /// If
        /// <code>context</code>
        /// is
        /// <code>null</code>
        /// , it is treated as
        /// <see cref="ParserRuleContext.EmptyContext"/>
        /// .
        /// </summary>
        /// <param name="stateNumber">the ATN state number</param>
        /// <param name="context">the full parse context</param>
        /// <returns>
        /// The set of potentially valid input symbols which could follow the
        /// specified state in the specified context.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// if the ATN does not contain a state with
        /// number
        /// <code>stateNumber</code>
        /// </exception>
        [return: NotNull]
        public virtual IntervalSet GetExpectedTokens(int stateNumber, RuleContext context)
        {
            if (stateNumber < 0 || stateNumber >= states.Count)
            {
                throw new ArgumentException("Invalid state number.");
            }
            RuleContext ctx = context;
            ATNState s = states[stateNumber];
            IntervalSet following = NextTokens(s);
            if (!following.Contains(TokenConstants.Epsilon))
            {
                return following;
            }
            IntervalSet expected = new IntervalSet();
            expected.AddAll(following);
            expected.Remove(TokenConstants.Epsilon);
            while (ctx != null && ctx.invokingState >= 0 && following.Contains(TokenConstants.Epsilon))
            {
                ATNState invokingState = states[ctx.invokingState];
                RuleTransition rt = (RuleTransition)invokingState.Transition(0);
                following = NextTokens(rt.followState);
                expected.AddAll(following);
                expected.Remove(TokenConstants.Epsilon);
                ctx = ctx.parent;
            }
            if (following.Contains(TokenConstants.Epsilon))
            {
                expected.Add(TokenConstants.Eof);
            }
            return expected;
        }
    }
}
