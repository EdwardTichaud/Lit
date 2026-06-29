from app.core.documentation import read_document
from app.core.document_selector import select_documents
from app.core.mission import MissionContext


def build_context(mission: MissionContext) -> MissionContext:

    selected = select_documents(mission.query, mission)

    for doc in selected:
        file_name = doc["file"]
        content = read_document(file_name)

        mission.loaded_documents.append({
            "file": file_name,
            "score": doc.get("score", 0),
            "title": doc.get("title", ""),
        })

        mission.llm_context += f"\n\n===== {file_name} =====\n\n"
        mission.llm_context += content

    return mission
