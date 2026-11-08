"""Compare final foot bones with IK goals in Animator-local space."""
import argparse
import csv
import json
import math
import statistics
from pathlib import Path


def read_rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def local_position(row, side, scale):
    rotation = [float(row[f"animator_q{axis}"]) for axis in "xyzw"]
    length = math.hypot(*rotation)
    x, y, z, w = [value / length for value in rotation]
    inverse = (
        (1 - 2 * (y*y + z*z), 2 * (x*y + z*w), 2 * (x*z - y*w)),
        (2 * (x*y - z*w), 1 - 2 * (x*x + z*z), 2 * (y*z + x*w)),
        (2 * (x*z + y*w), 2 * (y*z - x*w), 1 - 2 * (x*x + y*y)),
    )
    relative = [float(row[f"{side}_{axis}"]) - float(row[f"animator_{axis}"]) for axis in "xyz"]
    return [sum(a * b for a, b in zip(axis, relative)) / scale for axis in inverse]


def handoffs(poses, samples):
    results = []
    for before, after in zip(poses, poses[1:]):
        start, end = samples[before["frame"]], samples[after["frame"]]
        if start["trial"] != end["trial"] or start["base_state"] == end["base_state"]:
            continue
        if int(after["frame"]) - int(before["frame"]) != 1:
            continue
        distances = {part: math.dist([float(before[f"{part}_{axis}"]) for axis in "xyz"],
                                    [float(after[f"{part}_{axis}"]) for axis in "xyz"])
                     for part in ("root", "left", "right")}
        results.append({
            "trial": end["trial"], "frame": after["frame"],
            "from_clips": start["base_clips"], "to_clips": end["base_clips"],
            "from_phase": float(start["base_phase"]), "to_phase": float(end["base_phase"]),
            "qualified_before": start["support_blend"].lower() == "true",
            "qualified_after": end["support_blend"].lower() == "true",
            "dt": float(after["unscaled_time"]) - float(before["unscaled_time"]),
            "world_displacement_m": distances,
        })
    return results


def analyse(run, allow_partial):
    report = json.loads((run / "report.json").read_text(encoding="utf-8-sig"))
    if not allow_partial and (not report["status"].startswith("RECORDED") or report["errors"]):
        raise ValueError(f"{run}: incomplete or failed run ({report['status']})")
    all_samples = {row["frame"]: row for row in read_rows(run / "samples.csv")}
    samples = {frame: row for frame, row in all_samples.items()
               if row["support_blend"].lower() == "true"}
    trace = run / "pose-trace.csv"
    poses = [pose for pose in read_rows(trace if trace.exists() else Path(report["poseTrace"]))
             if pose["frame"] in all_samples]
    trials = {}
    matched = set()
    mismatched = set()
    for pose in poses:
        frame = pose["frame"]
        sample = samples.get(frame)
        if sample is None:
            continue
        if pose["ik_frame"] != frame or pose["ik_late_frame"] != frame:
            mismatched.add(frame)
            continue
        matched.add(frame)
        scale = float(sample["animator_scale"])
        if scale <= 0:
            raise ValueError(f"{run}: invalid Animator scale in frame {frame}")
        trial = trials.setdefault(sample["trial"], {"native": [], "final": []})
        for side in ("left", "right"):
            actual = local_position(pose, side, scale)
            for target, residuals in trial.items():
                residuals.append([1000 * (position - float(pose[f"{target}_{side}_{axis}"]))
                                  for axis, position in zip("xyz", actual)])

    def summarize(residuals):
        vertical = [y for _, y, _ in residuals]
        return {
            "feet": len(residuals),
            "min_y_mm": min(vertical),
            "median_y_mm": statistics.median(vertical),
            "max_y_mm": max(vertical),
            "max_horizontal_mm": max(math.hypot(x, z) for x, _, z in residuals),
        }

    if not matched or (not allow_partial and samples.keys() - matched):
        raise ValueError(f"{run}: joined {len(matched)} of {len(samples)} qualified poses")
    return {
        "run": report["run"],
        "status": report["status"],
        "scope": "Qualified goals with both IK passes stamped on the same frame. "
                 "Uniform-scale Animator-local bone errors. No sole, transition or visual verdict.",
        "matched_frames": len(matched),
        "missing_frames": len(samples.keys() - matched - mismatched),
        "wrong_pass_frames": len(mismatched),
        "state_handoffs": handoffs(poses, all_samples),
        "trials": {trial: {target: summarize(residuals) for target, residuals in targets.items()}
                   for trial, targets in trials.items()},
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--allow-partial", action="store_true")
    args = parser.parse_args()
    print(json.dumps(analyse(args.run, args.allow_partial), indent=2))
