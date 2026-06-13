#!/usr/bin/env python3
import os


def main():
    print(f"Hello {os.getenv('NAME', 'world')}")


if __name__ == '__main__':
    main()
