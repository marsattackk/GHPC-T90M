using GHPC.Equipment.Optics;
using GHPC.Weapons;
using Reticle;
using UnityEngine;

namespace T90M.Common
{
	// Has to run after UsableOptic.LateUpdate.
	[DefaultExecutionOrder(20)]
	internal sealed class ReticleRangeLine : MonoBehaviour
	{
		UsableOptic _optic;
		FireControlSystem _fcs;
		System.Func<AmmoType> _ammo;

		void LateUpdate()
		{
			if (_optic?.reticleMesh == null || _fcs == null || _ammo == null) return;
			var a = _ammo();
			float se = a != null && _fcs.CurrentRange > 0f ? BallisticComputerRepository.Instance.GetSuperelevation(a, _fcs.CurrentRange) : 0f;
			_optic.reticleMesh.SetAlignment(ReticleTree.GroupBase.Alignment.Impact, new AngularVector2(0f, se, AngularLength.AngularUnit.DEG));
		}
	}
}
