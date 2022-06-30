using UnityEngine;

namespace VRCF.Model {

[AddComponentMenu("")]
public class VRCFuryLegacy : MonoBehaviour {

    public VRCFuryConfig config;

    public VRCFuryState stateBlink;
    public AnimationClip viseme;

    public bool scaleEnabled;
    public int securityCodeLeft;
    public int securityCodeRight;

    public GameObject breatheObject;
    public string breatheBlendshape;
    public float breatheScaleMin;
    public float breatheScaleMax;

    public VRCFuryState stateToesDown;
    public VRCFuryState stateToesUp;
    public VRCFuryState stateToesSplay;

    public VRCFuryState stateEyesClosed;
    public VRCFuryState stateEyesHappy;
    public VRCFuryState stateEyesSad;
    public VRCFuryState stateEyesAngry;

    public VRCFuryState stateMouthBlep;
    public VRCFuryState stateMouthSuck;
    public VRCFuryState stateMouthSad;
    public VRCFuryState stateMouthAngry;
    public VRCFuryState stateMouthHappy;

    public VRCFuryState stateEarsBack;

    public VRCFuryState stateTalking;

    public VRCFuryProps props;
}

}
