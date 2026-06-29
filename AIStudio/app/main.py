from app.agents.architect import ArchitectAgent


def main():

    agent = ArchitectAgent()

    answer = agent.ask(
        "Je veux modifier le système de combat."
    )

    print(answer)


if __name__ == "__main__":
    main()