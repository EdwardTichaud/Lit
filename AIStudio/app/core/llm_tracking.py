import time

# Prix à ajuster selon ta page OpenAI.
# Valeurs en dollars pour 1 million de tokens.
MODEL_PRICING_USD_PER_1M = {
    "gpt-5.4": {
        "input": 5.00,
        "output": 30.00,
    },
}


def estimate_cost_usd(model: str, input_tokens: int, output_tokens: int) -> float:
    pricing = MODEL_PRICING_USD_PER_1M.get(model)

    if not pricing:
        return 0.0

    input_cost = (input_tokens / 1_000_000) * pricing["input"]
    output_cost = (output_tokens / 1_000_000) * pricing["output"]

    return input_cost + output_cost


def tracked_invoke(llm, messages, mission, label: str):
    start = time.time()

    response = llm.invoke(messages)

    duration = time.time() - start

    usage = getattr(response, "usage_metadata", None) or {}

    model = getattr(llm, "model_name", "unknown")

    input_tokens = usage.get("input_tokens", 0)
    output_tokens = usage.get("output_tokens", 0)
    total_tokens = usage.get("total_tokens", 0)

    estimated_cost = estimate_cost_usd(
        model,
        input_tokens,
        output_tokens,
    )

    mission.llm_calls.append({
        "label": label,
        "model": model,
        "input_tokens": input_tokens,
        "output_tokens": output_tokens,
        "total_tokens": total_tokens,
        "estimated_cost_usd": estimated_cost,
        "duration_seconds": round(duration, 2),
    })

    return response


def print_llm_diagnostics(mission):
    print("\n========== DIAGNOSTIC API ==========\n")

    total_input = 0
    total_output = 0
    total_tokens = 0
    total_duration = 0
    total_cost = 0.0

    for call in mission.llm_calls:
        total_input += call["input_tokens"]
        total_output += call["output_tokens"]
        total_tokens += call["total_tokens"]
        total_duration += call["duration_seconds"]
        total_cost += call["estimated_cost_usd"]

        print(f"Appel : {call['label']}")
        print(f"Modèle : {call['model']}")
        print(f"Input tokens : {call['input_tokens']}")
        print(f"Output tokens : {call['output_tokens']}")
        print(f"Total tokens : {call['total_tokens']}")
        print(f"Coût estimé : ${call['estimated_cost_usd']:.6f}")
        print(f"Durée : {call['duration_seconds']} s")
        print("")

    print("TOTAL")
    print(f"Input tokens : {total_input}")
    print(f"Output tokens : {total_output}")
    print(f"Total tokens : {total_tokens}")
    print(f"Coût estimé : ${total_cost:.6f}")
    print(f"Durée totale : {round(total_duration, 2)} s")