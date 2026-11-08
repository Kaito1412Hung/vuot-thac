FROM python:3.10.12-slim
WORKDIR /workspace
COPY requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir -r /tmp/requirements.txt
COPY learn.py /opt/vuotthac/learn.py
EXPOSE 5004 6006
ENTRYPOINT ["python", "/opt/vuotthac/learn.py"]
CMD ["Training/config/boat_ppo.yaml", "--run-id=suoirung-docker", "--results-dir=Training/results", "--base-port=5004"]
