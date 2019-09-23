﻿using NBitcoin;
using System.Linq;
using NBXplorer.DerivationStrategy;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NBitcoin.Crypto;
using System.Threading.Tasks;

namespace NBXplorer.DerivationStrategy
{
	public class MultisigPlusOneDerivationStrategy : DerivationStrategyBase
	{
		internal MultisigDerivationStrategy Multisig
		{
			get; set;
		}
		internal DirectDerivationStrategy One
		{
			get; set;
		}
		public bool LexicographicOrder
		{
			get; set;
		}

		protected override string StringValue
		{
			get
			{
				StringBuilder builder = new StringBuilder();
				builder.Append(Multisig.ToString() + "-and-" + One.ToString());
				if(IsLegacy)
				{
					builder.Append("-[legacy]");
				}
				if(!LexicographicOrder)
				{
					builder.Append("-[keeporder]");
				}
				return builder.ToString();
			}
		}

		internal MultisigPlusOneDerivationStrategy(MultisigDerivationStrategy multisig, DirectDerivationStrategy one, bool isLegacy)
		{
			Multisig = Clone(multisig);
			One = Clone(one);
			LexicographicOrder = true;
			IsLegacy = isLegacy;
		}

		private DirectDerivationStrategy Clone(DirectDerivationStrategy one)
		{
			return new DirectDerivationStrategy(one.BitcoinRoot) { Segwit = true };
		}

		static MultisigDerivationStrategy Clone(MultisigDerivationStrategy multisig)
		{
			return new MultisigDerivationStrategy(multisig.RequiredSignatures, multisig.Keys, false) { LexicographicOrder = true };
		}

		public bool IsLegacy
		{
			get; private set;
		}

		public override Derivation GetDerivation()
		{
			var pubKeys = new PubKey[this.Multisig.Keys.Length];
			Parallel.For(0, pubKeys.Length, i =>
			{
				pubKeys[i] = this.Multisig.Keys[i].ExtPubKey.PubKey;
			});

			if (LexicographicOrder)
			{
				Array.Sort(pubKeys, MultisigDerivationStrategy.LexicographicComparer);
			}
			List<Op> ops = new List<Op>();
			ops.Add(Op.GetPushOp(One.Root.PubKey.ToBytes()));
			ops.Add(OpcodeType.OP_CHECKSIGVERIFY);
			ops.Add(Op.GetPushOp(Multisig.RequiredSignatures));
			foreach (var keys in pubKeys)
			{
				ops.Add(Op.GetPushOp(keys.ToBytes()));
			}
			ops.Add(Op.GetPushOp(pubKeys.Length));
			ops.Add(OpcodeType.OP_CHECKMULTISIG);

			return new Derivation() { ScriptPubKey = new Script(ops.ToList()) };
		}

		public override IEnumerable<ExtPubKey> GetExtPubKeys()
		{
			return this.Multisig.GetExtPubKeys();
		}

		public override DerivationStrategyBase GetChild(KeyPath keyPath)
		{
			return new MultisigPlusOneDerivationStrategy((MultisigDerivationStrategy)Multisig.GetChild(keyPath), (DirectDerivationStrategy)One.GetChild(keyPath), IsLegacy);
		}
	}
}
