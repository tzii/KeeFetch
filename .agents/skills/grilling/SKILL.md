---
name: grilling
description: Stress-test a plan, architecture choice, or risky change before implementation. Use when the user asks to grill, challenge, or interrogate a design.
disable-model-invocation: true
---

# Grilling

The goal is to remove silent assumptions before code is written.

1. Read the relevant repo docs, code, tests, issues, and recent changes first. Facts that can be looked up are the agent's job, not questions for the user.
2. Model the design as decisions with dependencies. Ask only questions whose prerequisites are already known.
3. Ask the current decision frontier in one round. Number the questions and give a recommended answer for each, with the tradeoff that recommendation accepts.
4. After the user's answers, update the decision tree and continue until no meaningful branch is unresolved.
5. Separate facts, constraints, preferences, and guesses. Never present a guess as a repo fact.
6. Do not implement the grilled plan until the user says the design is settled or asks to proceed.

A good grilling session ends with explicit choices, non-goals, and acceptance criteria, not a longer brainstorm.
