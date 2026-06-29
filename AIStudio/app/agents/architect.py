from app.core.documentation import (
    find_by_text,
    read_document,
)

from app.core.models import get_llm
from app.core.prompts import load_prompt


class ArchitectAgent:

    def __init__(self):

        self.llm = get_llm()
        self.system_prompt = load_prompt("architect.md")

    def ask(self, question: str):

        docs = find_by_text(question)

        context = ""

        for doc in docs:

            context += (
                f"\n\n===== {doc['file']} =====\n\n"
            )

            context += read_document(doc["file"])

        response = self.llm.invoke(
            [
                (
                    "system",
                    self.system_prompt,
                ),
                (
                    "user",
                    f"""
Question :

{question}

Documentation :

{context}
""",
                ),
            ]
        )

        return response.content