from app.core.documentation_indexer import write_documentation_index


def main():
    path = write_documentation_index()
    print(f"Documentation index generated: {path}")


if __name__ == "__main__":
    main()