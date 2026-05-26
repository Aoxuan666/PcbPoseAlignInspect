using System;

namespace PcbPoseAlignInspect.Models
{
	[Flags]
	public enum InspectNgReason
	{
		None = 0,
		ParameterInvalid = 1,
		FeatureMatchFailed = 2,
		BoardDetectFailed = 4,
		XOutOfTolerance = 8,
		YOutOfTolerance = 0x10,
		AngleOutOfTolerance = 0x20,
		UnifiedToleranceOut = 0x40,
		AlgorithmException = 0x80
	}
}
